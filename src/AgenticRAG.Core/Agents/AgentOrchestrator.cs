// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// AgentOrchestrator — The brain of the Agentic RAG system (MCP-Only).
//
// ALL tool calls now route through the MCP server — no direct calls.
//
// Architecture (MCP-Only):
//   BEFORE: GPT-4o → FunctionInvocation → DocumentSearchTool (direct)
//   NOW:    GPT-4o → FunctionInvocation → McpToolProxyService
//           → HTTP /mcp → AgenticRagMcpServer → DocumentSearchTool
//
// Why MCP-only?
//   1. Protocol standardization — every tool call flows through MCP,
//      so the same /mcp endpoint serves both GPT-4o (internal) and
//      external MCP clients (Claude, VS Code, Copilot, Gemini, etc.)
//   2. Decoupled architecture — this orchestrator has ZERO dependencies
//      on concrete tool classes (DocumentSearchTool, SqlQueryTool, etc.).
//      It only depends on McpToolProxyService, which speaks MCP protocol.
//   3. Tool portability — tools can be moved to a remote MCP server,
//      versioned independently, or swapped out without touching this class.
//   4. Single audit point — all tool invocations go through /mcp,
//      making logging, metering, and access control simpler.
//
// The original direct-call version is preserved in AgentOrchestrator.Backup.cs.
//
// Flow: Question → Cache check → Load memory → Build prompt → GPT-4o
//       auto-calls tools (via FunctionInvocation → McpToolProxyService → /mcp)
//       → Reflection score → Retry if low → Cache result → Save memory → Return
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticRAG.Core.Caching;
using AgenticRAG.Core.Configuration;
using AgenticRAG.Core.Memory;
using AgenticRAG.Core.Models;
using AgenticRAG.Core.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticRAG.Core.Agents;

public class AgentOrchestrator
{
    // ── Dependencies ──
    // IChatClient: GPT-4o wrapped with FunctionInvocationChatClient middleware.
    //   When GPT-4o emits a tool_call, the middleware intercepts it, calls the
    //   matching AIFunction (registered in ChatOptions), and feeds the result back.
    private readonly IChatClient _chatClient;

    // McpToolProxyService: The ONLY tool dependency. Every tool call from GPT-4o
    // is routed through this service → MCP HTTP transport → /mcp endpoint →
    // AgenticRagMcpServer → actual tool classes. No direct tool references here.
    private readonly McpToolProxyService _mcpProxy;

    private readonly ConversationMemoryService _memoryService;  // Redis-backed session history
    private readonly SemanticCacheService _cacheService;        // Semantic similarity cache
    private readonly ReflectionService _reflectionService;      // Quality scoring (1-10)
    private readonly AgentSettings _settings;
    private readonly ILogger<AgentOrchestrator> _logger;

    // ── Constructor ──
    // Note: No ChatOptions injected — we build ChatOptions on-the-fly using
    // McpToolProxyService methods registered as AIFunction tools.
    // No DocumentSearchTool, SqlQueryTool, ImageCitationTool — all via MCP.
    public AgentOrchestrator(
        IChatClient chatClient,
        McpToolProxyService mcpProxy,
        ConversationMemoryService memoryService,
        SemanticCacheService cacheService,
        ReflectionService reflectionService,
        AgentSettings settings,
        ILogger<AgentOrchestrator> logger)
    {
        _chatClient = chatClient;
        _mcpProxy = mcpProxy;
        _memoryService = memoryService;
        _cacheService = cacheService;
        _reflectionService = reflectionService;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Main entry point — orchestrates the full agent pipeline for a single question.
    /// All tool execution now goes through MCP — no direct tool class calls.
    /// Steps: Cache → Memory → Build MCP tools → GPT-4o execution → Reflection → Cache → Memory
    /// </summary>
    public async Task<AgentResponse> ProcessAsync(AgentRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N")[..12];
        _logger.LogInformation("Processing question for session {SessionId}", sessionId);

        // ── STEP 1: Check Semantic Cache ──
        // Embeds the question and searches the cache index for cosine similarity ≥ 0.92.
        // On hit, skips the entire pipeline — returns instantly (~150ms vs 4-8s).
        var cachedAnswer = await _cacheService.TryGetCachedAnswerAsync(request.Question);
        if (cachedAnswer != null && IsUsableCachedAnswer(cachedAnswer))
        {
            _logger.LogInformation("Cache HIT for question");
            cachedAnswer.FromCache = true;
            cachedAnswer.SessionId = sessionId;
            return cachedAnswer;
        }
        if (cachedAnswer != null)
        {
            _logger.LogWarning("Ignoring low-quality cached answer and regenerating fresh response");
        }

        // ── STEP 2: Load Conversation Memory ──
        // Fetches past turns from Redis. If history > SummarizeAfterTurns, the older
        // turns get LLM-summarized to save tokens while preserving context.
        var history = await _memoryService.GetHistoryAsync(sessionId);

        // ── STEP 3: Build Chat Messages ──
        var messages = new List<ChatMessage>();
        messages.Add(new ChatMessage(ChatRole.System, GetSystemPrompt()));

        foreach (var turn in history)
        {
            messages.Add(new ChatMessage(
                turn.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                turn.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Question));

        // ── STEP 4: Build ChatOptions with MCP-proxied tools ──
        // Every tool is created from McpToolProxyService methods. When GPT-4o
        // calls any of these, the FunctionInvocationChatClient middleware executes
        // the proxy method, which sends the call to /mcp via MCP protocol.
        //
        // Call chain: GPT-4o tool_call → FunctionInvocation middleware
        //   → McpToolProxyService.SearchDocumentsAsync() → MCP HTTP /mcp
        //   → AgenticRagMcpServer.SearchDocumentsAsync() → DocumentSearchTool (actual)
        var chatOptions = BuildMcpChatOptions();

        // ── STEP 5: Agent Execution (Plan → Execute Tools via MCP → Reason) ──
        // FunctionInvocationChatClient middleware lets GPT-4o autonomously decide
        // which tools to call. Every tool invocation flows through MCP now:
        //   GPT-4o decides → proxy calls /mcp → MCP server executes → result returns
        // The ReAct pattern (Reason → Act → Observe) runs automatically — GPT-4o
        // may call multiple tools in sequence, re-plan based on results, then synthesize.
        var toolsUsed = new List<string>();
        var reasoningSteps = new List<string>();

        var response = await _chatClient.GetResponseAsync(messages, chatOptions);

        // Extract tool calls from the response message chain
        ExtractToolCalls(response.Messages, toolsUsed, reasoningSteps);

        // Add response messages back to the conversation for subsequent calls
        foreach (var rm in response.Messages)
            messages.Add(rm);

        var answer = response.Text ?? "I was unable to generate a response.";

        // ── STEP 6: Reflection (Self-Correction) ──
        // Separate LLM call scores answer quality 1-10 on: grounded, complete, cited, clear.
        // If score < threshold, the agent retries with a refinement prompt.
        // This catches ~30% of poor answers that Classic RAG would return as-is.
        var reflectionScore = await _reflectionService.EvaluateAsync(
            request.Question, answer, toolsUsed);

        int retries = 0;
        while (reflectionScore < _settings.ReflectionThreshold
               && retries < _settings.MaxReflectionRetries)
        {
            _logger.LogWarning("Reflection score {Score}/10 — retrying (attempt {Retry})",
                reflectionScore, retries + 1);

            reasoningSteps.Add($"Reflection: Score {reflectionScore}/10 — refining answer...");

            messages.Add(new ChatMessage(ChatRole.User,
                "Your previous answer scored low on completeness. " +
                "Please search for additional information and provide a more thorough answer " +
                "with better citations."));

            // Retry also uses MCP-only tools — same chatOptions, same proxy path
            response = await _chatClient.GetResponseAsync(messages, chatOptions);
            ExtractToolCalls(response.Messages, toolsUsed, reasoningSteps);
            foreach (var rm in response.Messages)
                messages.Add(rm);
            answer = response.Text ?? answer;
            reflectionScore = await _reflectionService.EvaluateAsync(
                request.Question, answer, toolsUsed);
            retries++;
        }

        // ── STEP 7: Build Response ──
        var agentResponse = new AgentResponse
        {
            Answer = answer,
            ToolsUsed = toolsUsed.Distinct().ToList(),
            ReasoningSteps = reasoningSteps,
            ReflectionScore = reflectionScore,
            SessionId = sessionId,
            TokenUsage = new TokenUsageInfo { ToolCallCount = toolsUsed.Count }
        };

        agentResponse.TextCitations = ParseTextCitations(answer);
        agentResponse.ImageCitations = ParseImageCitations(answer);

        // ── STEP 8: Cache the Response ──
        // Only cache high-quality answers (passed reflection + has tool calls).
        // Next time someone asks a semantically similar question, it returns instantly.
        if (IsUsableCachedAnswer(agentResponse))
        {
            await _cacheService.CacheAnswerAsync(request.Question, agentResponse);
        }
        else
        {
            _logger.LogWarning("Skipping cache write for low-quality answer");
        }

        // ── STEP 9: Save to Conversation Memory ──
        await _memoryService.AddTurnAsync(sessionId, "user", request.Question);
        await _memoryService.AddTurnAsync(sessionId, "assistant", answer);

        return agentResponse;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // BuildMcpChatOptions — Registers ALL tools as AIFunctions wrapping
    // McpToolProxyService methods. Each AIFunction, when invoked by
    // FunctionInvocationChatClient, sends the call through the MCP protocol
    // to the /mcp endpoint instead of calling tool classes directly.
    //
    // This is the core of the MCP-only architecture: the orchestrator
    // knows nothing about DocumentSearchTool, SqlQueryTool, etc.
    // It only knows MCP proxy method names.
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private ChatOptions BuildMcpChatOptions()
    {
        return new ChatOptions
        {
            Tools = new List<AITool>
            {
                // Each tool wraps a McpToolProxyService method.
                // The proxy method → HTTP /mcp → AgenticRagMcpServer → real tool.
                AIFunctionFactory.Create(_mcpProxy.SearchDocumentsAsync),    // MCP → search_documents
                AIFunctionFactory.Create(_mcpProxy.QuerySqlAsync),           // MCP → query_sql
                AIFunctionFactory.Create(_mcpProxy.GetSchemaAsync),          // MCP → get_schema
                AIFunctionFactory.Create(_mcpProxy.GetDocumentImagesAsync),  // MCP → get_document_images
                AIFunctionFactory.Create(_mcpProxy.SearchWebAsync)           // MCP → search_web
            },
            Temperature = 0.1f
        };
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // System Prompt — Updated tool names to match McpToolProxyService methods.
    // GPT-4o sees these as the function names it can call. Under the hood,
    // each one routes through MCP, but GPT-4o doesn't need to know that.
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private static string GetSystemPrompt() => """
        You are an intelligent enterprise assistant with access to multiple data sources.
        All tool calls are routed through MCP (Model Context Protocol) for standardized access.

        AVAILABLE TOOLS (all via MCP):
        - SearchDocumentsAsync: Search contracts, policies, reports in the document index
        - QuerySqlAsync: Query billing, invoice, and vendor data from SQL Server
        - GetSchemaAsync: Get column names and types for SQL views (call FIRST if unsure)
        - GetDocumentImagesAsync: Get downloadable images/charts from documents
        - SearchWebAsync: Search the public internet for latest/public information

        RULES:
        1. ALWAYS search/query before answering — never make up information.
        2. For document content (clauses, terms, policies) → use SearchDocumentsAsync.
        3. For financial data (billing, invoices, amounts) → use QuerySqlAsync.
        4. For visual content (charts, diagrams) → use GetDocumentImagesAsync.
        5. For latest/public internet info not in internal sources → use SearchWebAsync.
        6. If a question needs BOTH document and SQL data → call both tools.
        7. Cite every fact: [DocSource N] for documents, [SQLSource] for SQL data, [WebSource N] for web.
        8. If you need SQL schema info, call GetSchemaAsync FIRST before writing a query.
        9. For comparisons, make separate tool calls for each item being compared.
        10. If results are insufficient, try a different search query.
        11. Present financial data in tables when there are 3+ rows.

        ANSWER FORMAT:
        - Start with a direct answer to the question.
        - Use bullet points for multi-part answers.
        - Include [DocSource N], [SQLSource], or [WebSource N] citations inline.
        - End with "Sources used:" summary listing all sources.
        - If images are relevant, note: [Image: filename] with download link.
        """;

    /// Extracts [DocSource N], [SQLSource], and [WebSource N] markers from the answer text.
    private static List<TextCitation> ParseTextCitations(string answer)
    {
        var citations = new List<TextCitation>();

        // Document citations: [DocSource 1], [DocSource 2], etc.
        var docMatches = Regex.Matches(answer, @"\[DocSource (\d+)\]");
        foreach (Match m in docMatches)
        {
            citations.Add(new TextCitation
            {
                Index = int.Parse(m.Groups[1].Value),
                SourceType = "document"
            });
        }

        // SQL citations: [SQLSource]
        if (answer.Contains("[SQLSource]"))
            citations.Add(new TextCitation { SourceType = "sql" });

        // Web citations: [WebSource 1], [WebSource 2], etc.
        var webMatches = Regex.Matches(answer, @"\[WebSource (\d+)\]");
        foreach (Match m in webMatches)
        {
            citations.Add(new TextCitation
            {
                Index = int.Parse(m.Groups[1].Value),
                SourceType = "web"
            });
        }

        return citations.DistinctBy(c => $"{c.SourceType}-{c.Index}").ToList();
    }

    /// Extracts [Image: filename] markers from the answer text into downloadable image references.
    private static List<ImageCitation> ParseImageCitations(string answer)
    {
        var images = new List<ImageCitation>();
        var matches = Regex.Matches(answer, @"\[Image[: ]+([^\]]+)\]");
        int i = 1;
        foreach (Match m in matches)
        {
            images.Add(new ImageCitation
            {
                Index = i++,
                FileName = m.Groups[1].Value.Trim()
            });
        }
        return images;
    }

    /// Inspects the response message chain to identify which tools GPT-4o called
    /// and in what order. Tool names here are the McpToolProxyService method names
    /// (e.g., "SearchDocumentsAsync"), which internally route through MCP.
    private void ExtractToolCalls(IList<ChatMessage> responseMessages,
        List<string> toolsUsed, List<string> reasoningSteps)
    {
        foreach (var msg in responseMessages)
        {
            foreach (var call in msg.Contents.OfType<FunctionCallContent>())
            {
                toolsUsed.Add(call.Name);
                reasoningSteps.Add($"Calling tool (via MCP): {call.Name}");
                _logger.LogInformation("Agent called tool via MCP: {Tool}", call.Name);
            }
            if (msg.Contents.OfType<FunctionResultContent>().Any())
                reasoningSteps.Add("MCP tool returned results");
        }
    }

    /// Guards against caching bad answers — requires non-empty answer,
    /// passing reflection score, and at least one tool invocation.
    private bool IsUsableCachedAnswer(AgentResponse response)
    {
        if (response == null)
            return false;

        if (string.IsNullOrWhiteSpace(response.Answer))
            return false;

        if (response.ReflectionScore < _settings.ReflectionThreshold)
            return false;

        if (response.ToolsUsed == null || response.ToolsUsed.Count == 0)
            return false;

        return true;
    }
}
