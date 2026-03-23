using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using AgenticRag.Agent.Configuration;
using AgenticRag.Agent.Interfaces;
using AgenticRag.DataAccess.Configuration;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ClientModel;

namespace AgenticRag.Agent.Services;

/// <summary>
/// Implements the agentic RAG reasoning loop using Azure OpenAI function/tool calling.
/// Supports multi-step reasoning, tool chaining (document search, SQL, web search),
/// caching, and memorization. Follows the Microsoft Agent Framework patterns.
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly ChatClient _chatClient;
    private readonly IChatThreadService _chatThreadService;
    private readonly IMemoryService _memoryService;
    private readonly CacheService _cacheService;
    private readonly IEnumerable<IAgentTool> _tools;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IOptions<AzureOpenAiOptions> openAiOptions,
        IChatThreadService chatThreadService,
        IMemoryService memoryService,
        CacheService cacheService,
        IEnumerable<IAgentTool> tools,
        IOptions<AgentOptions> agentOptions,
        ILogger<AgentOrchestrator> logger)
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(openAiOptions.Value.Endpoint),
            new ApiKeyCredential(openAiOptions.Value.ApiKey));
        _chatClient = azureClient.GetChatClient(openAiOptions.Value.ChatDeployment);

        _chatThreadService = chatThreadService;
        _memoryService = memoryService;
        _cacheService = cacheService;
        _tools = tools;
        _options = agentOptions.Value;
        _logger = logger;
    }

    public async Task<AgentResponse> RunAsync(
        string userQuery,
        string threadId,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_options.EnableCaching && _cacheService.TryGetAgentResponse(userQuery, out var cached) && cached != null)
        {
            _logger.LogInformation("Returning cached response for query: {Query}", userQuery);
            cached.FromCache = true;
            return cached;
        }

        var thread = await _chatThreadService.GetThreadAsync(threadId, cancellationToken);
        var memory = await _memoryService.GetMemoryAsync("default", cancellationToken);

        var messages = BuildMessageHistory(thread, memory, userQuery);
        var chatTools = BuildChatTools();

        var toolsUsed = new List<ToolUsage>();
        var reasoningTrace = new StringBuilder();
        var allCitations = new List<Citation>();

        // Agentic reasoning loop
        for (int step = 0; step < _options.MaxReasoningSteps; step++)
        {
            _logger.LogInformation("Agent reasoning step {Step}/{Max}", step + 1, _options.MaxReasoningSteps);

            var chatOptions = new ChatCompletionOptions();
            foreach (var tool in chatTools)
            {
                chatOptions.Tools.Add(tool);
            }

            var response = await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
            var completion = response.Value;

            if (completion.FinishReason == ChatFinishReason.Stop)
            {
                // Final answer
                var answer = completion.Content[0].Text;
                reasoningTrace.AppendLine($"[Step {step + 1}] Final answer generated.");

                var agentResponse = new AgentResponse
                {
                    Answer = answer,
                    Citations = allCitations,
                    ToolsUsed = toolsUsed,
                    ReasoningTrace = _options.EnableReasoningTrace ? reasoningTrace.ToString() : string.Empty
                };

                if (_options.EnableCaching)
                {
                    _cacheService.SetAgentResponse(userQuery, agentResponse);
                }

                // Persist to thread
                await _chatThreadService.AddMessageAsync(threadId, new AgenticRag.Shared.Models.ChatMessage
                {
                    Role = MessageRole.User,
                    Content = userQuery
                }, cancellationToken);

                await _chatThreadService.AddMessageAsync(threadId, new AgenticRag.Shared.Models.ChatMessage
                {
                    Role = MessageRole.Assistant,
                    Content = answer,
                    Citations = allCitations
                }, cancellationToken);

                return agentResponse;
            }

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Execute tool calls
                messages.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    var tool = _tools.FirstOrDefault(t =>
                        string.Equals(t.Name, toolCall.FunctionName, StringComparison.OrdinalIgnoreCase));

                    if (tool == null)
                    {
                        _logger.LogWarning("Unknown tool requested: {ToolName}", toolCall.FunctionName);
                        messages.Add(new ToolChatMessage(toolCall.Id, $"Tool '{toolCall.FunctionName}' not found."));
                        continue;
                    }

                    var sw = Stopwatch.StartNew();
                    var toolInput = toolCall.FunctionArguments.ToString();
                    var toolOutput = await tool.ExecuteAsync(toolInput, cancellationToken);
                    sw.Stop();

                    reasoningTrace.AppendLine($"[Step {step + 1}] Tool '{tool.Name}' called with: {toolInput}");

                    toolsUsed.Add(new ToolUsage
                    {
                        ToolName = tool.Name,
                        Input = toolInput,
                        Output = toolOutput,
                        DurationMs = sw.ElapsedMilliseconds
                    });

                    // Extract citations from document_search results
                    if (tool.Name == "document_search")
                    {
                        allCitations.AddRange(ExtractCitations(toolOutput));
                    }

                    messages.Add(new ToolChatMessage(toolCall.Id, toolOutput));
                }
            }
        }

        // Max steps reached — return what we have
        _logger.LogWarning("Max reasoning steps ({Max}) reached for query: {Query}", _options.MaxReasoningSteps, userQuery);
        return new AgentResponse
        {
            Answer = "I was unable to produce a complete answer within the reasoning step limit. Please try a more specific question.",
            Citations = allCitations,
            ToolsUsed = toolsUsed,
            ReasoningTrace = _options.EnableReasoningTrace ? reasoningTrace.ToString() : string.Empty
        };
    }

    private List<OpenAI.Chat.ChatMessage> BuildMessageHistory(ChatThread thread, UserMemory memory, string userQuery)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>();

        var systemPrompt = BuildSystemPrompt(memory);
        messages.Add(new SystemChatMessage(systemPrompt));

        // Include recent history (up to MaxChatHistoryMessages)
        var recentMessages = thread.Messages
            .TakeLast(_options.MaxChatHistoryMessages)
            .ToList();

        foreach (var msg in recentMessages)
        {
            if (msg.Role == MessageRole.User)
                messages.Add(new UserChatMessage(msg.Content));
            else if (msg.Role == MessageRole.Assistant)
                messages.Add(new AssistantChatMessage(msg.Content));
        }

        messages.Add(new UserChatMessage(userQuery));
        return messages;
    }

    private static string BuildSystemPrompt(UserMemory memory)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an intelligent RAG assistant. You have access to the following tools:");
        sb.AppendLine("- document_search: search indexed documents");
        sb.AppendLine("- sql_query: run SELECT queries on the SQL database");
        sb.AppendLine("- web_search: search the public web");
        sb.AppendLine();
        sb.AppendLine("Always use tools to ground your answers. Cite sources. Avoid hallucination.");
        sb.AppendLine("Think step by step before responding. Use multiple tools when needed.");

        if (memory.ImportantFacts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Important facts about the user:");
            foreach (var fact in memory.ImportantFacts)
                sb.AppendLine($"- {fact}");
        }

        if (memory.Preferences.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("User preferences:");
            foreach (var (key, value) in memory.Preferences)
                sb.AppendLine($"- {key}: {value}");
        }

        return sb.ToString();
    }

    private IEnumerable<ChatTool> BuildChatTools()
    {
        return _tools.Select(t => ChatTool.CreateFunctionTool(
            functionName: t.Name,
            functionDescription: t.Description,
            functionParameters: BinaryData.FromString("""{"type":"object","properties":{"input":{"type":"string"}},"required":["input"]}""")));
    }

    private static IEnumerable<Citation> ExtractCitations(string toolOutput)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolOutput);
            return doc.RootElement.EnumerateArray().Select(r => new Citation
            {
                FileName = r.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? string.Empty : string.Empty,
                PageNumber = r.TryGetProperty("pageNumber", out var pn) ? pn.GetInt32() : 0,
                Section = r.TryGetProperty("section", out var sec) ? sec.GetString() ?? string.Empty : string.Empty,
                Snippet = r.TryGetProperty("content", out var content) ? (content.GetString() ?? string.Empty)[..Math.Min(200, content.GetString()?.Length ?? 0)] : string.Empty,
            }).ToList();
        }
        catch
        {
            return Enumerable.Empty<Citation>();
        }
    }
}
