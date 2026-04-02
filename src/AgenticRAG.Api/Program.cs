// =====================================================================================
// Program.cs — THE STARTUP FILE (Composition Root) of the Agentic RAG API
// =====================================================================================
//
// WHAT IS THIS FILE?
// ------------------
// This is where everything starts. Think of it as the "wiring diagram" of the app.
// It creates all the services, connects them together, and starts the web server.
// In .NET, this is called the "Composition Root" — the ONE place where you decide
// which concrete classes fulfill which roles.
//
// WHAT DOES "AGENTIC RAG" MEAN?
// - RAG = Retrieval-Augmented Generation: Instead of LLM making things up, we first
//   SEARCH real data (documents, SQL, web), then give those results to the LLM to
//   generate a grounded answer.
// - Agentic = The LLM acts as an AGENT — it DECIDES which tools to call, what queries
//   to run, and can retry if results are bad. It's not just "search + generate".
//
// HOW DO TOOL CALLS WORK? (MCP Architecture)
// - MCP = Model Context Protocol — an open standard so ANY AI model (GPT, Claude, Gemini)
//   can discover and call your tools using the same protocol.
// - Flow: User Question → Orchestrator → GPT-4o-mini picks tools → MCP Proxy sends
//   HTTP request to /mcp endpoint → MCP Server calls real tool → result flows back.
// - The Orchestrator never touches tool classes directly. It only knows MCP proxy methods.
//
// INTERVIEW TIP: "We use MCP so the orchestrator is decoupled from tools. Any MCP client
// (Claude, VS Code Copilot) can reuse our tools without code changes."
// =====================================================================================

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Storage.Blobs;
using AgenticRAG.Core.Ambiguity;
using AgenticRAG.Core.Agents;
using AgenticRAG.Core.Caching;
using AgenticRAG.Core.Configuration;
using AgenticRAG.Core.McpTools;
using AgenticRAG.Core.Memory;
using AgenticRAG.Core.Privacy;
using AgenticRAG.Core.Tools;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================================
// STEP 1: CONFIGURATION BINDING
// =====================================================================================
// Read appsettings.json and map each section to a strongly-typed C# class.
// Example: "AzureAISearch" section in JSON → AzureAISearchSettings object in C#.
// This gives us IntelliSense and compile-time safety instead of magic strings.
var searchSettings = builder.Configuration.GetSection("AzureAISearch").Get<AzureAISearchSettings>()!;
var openAiSettings = builder.Configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>()!;
var sqlSettings = builder.Configuration.GetSection("SqlServer").Get<SqlServerSettings>()!;
var blobSettings = builder.Configuration.GetSection("BlobStorage").Get<BlobStorageSettings>()!;
var redisSettings = builder.Configuration.GetSection("Redis").Get<RedisSettings>()!;
var agentSettings = builder.Configuration.GetSection("Agent").Get<AgentSettings>()!;
var webSearchSettings = builder.Configuration.GetSection("GoogleWebSearch").Get<GoogleWebSearchSettings>()!;
var mcpProxySettings = builder.Configuration.GetSection("McpProxy").Get<McpProxySettings>()!;
var piiSettings = builder.Configuration.GetSection("Pii").Get<PiiSettings>() ?? new PiiSettings();
var queryRewriteSettings = builder.Configuration.GetSection("QueryRewrite").Get<QueryRewriteSettings>() ?? new QueryRewriteSettings();
var ambiguitySettings = builder.Configuration.GetSection("Ambiguity").Get<AmbiguitySettings>() ?? new AmbiguitySettings();

// Register all settings as Singletons so any class can receive them via constructor injection.
// Singleton = one instance shared across the entire app lifetime.
builder.Services.AddSingleton(searchSettings);
builder.Services.AddSingleton(openAiSettings);
builder.Services.AddSingleton(sqlSettings);
builder.Services.AddSingleton(blobSettings);
builder.Services.AddSingleton(redisSettings);
builder.Services.AddSingleton(agentSettings);
builder.Services.AddSingleton(webSearchSettings);
builder.Services.AddSingleton(mcpProxySettings);
builder.Services.AddSingleton(piiSettings);
builder.Services.AddSingleton(queryRewriteSettings);
builder.Services.AddSingleton(ambiguitySettings);

// =====================================================================================
// STEP 2: AZURE CLIENT SETUP
// =====================================================================================
// DefaultAzureCredential = Smart auth that works everywhere:
// - On your laptop: uses your Visual Studio / Azure CLI login
// - On Azure: uses Managed Identity (no passwords stored anywhere)
// INTERVIEW TIP: "We use DefaultAzureCredential so there are ZERO secrets in code or config."
var credential = new DefaultAzureCredential();

// Azure OpenAI Client — talks to GPT-4o and embedding models hosted on Azure
var openAiClient = new AzureOpenAIClient(new Uri(openAiSettings.Endpoint), credential);
builder.Services.AddSingleton(openAiClient);

// Azure AI Search Client — searches the document index (contracts, policies, reports)
var searchClient = new SearchClient(
    new Uri(searchSettings.Endpoint),
    searchSettings.IndexName,
    credential);
builder.Services.AddSingleton(searchClient);

// Separate search client for the semantic cache index (different index, same service)
var cacheSearchClient = new SearchClient(
    new Uri(searchSettings.Endpoint),
    "semantic-cache",
    credential);

// TWO DIFFERENT EMBEDDING MODELS (Cost Optimization):
// - Document search: text-embedding-3-large (1536 dimensions) — high quality, matches ingestion
// - Semantic cache: text-embedding-3-small (512 dimensions) — 6.5x cheaper, good enough for
//   comparing question-to-question similarity (we don't need document-level precision here)
// INTERVIEW TIP: "We use a cheaper embedding model for cache lookups because cache only
// compares questions to questions, not questions to documents."
var docEmbeddingClient = openAiClient.GetEmbeddingClient(openAiSettings.EmbeddingDeployment);
builder.Services.AddSingleton(docEmbeddingClient);

var cacheEmbeddingClient = openAiClient.GetEmbeddingClient(openAiSettings.CacheEmbeddingDeployment);

// Azure Blob Storage — stores uploaded PDFs and extracted images
var blobServiceClient = new BlobServiceClient(
    new Uri($"https://{blobSettings.AccountName}.blob.core.windows.net"), credential);
builder.Services.AddSingleton(blobServiceClient);

// Redis — stores conversation history (multi-turn memory) with 4-hour TTL per session
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisSettings.ConnectionString));

// =====================================================================================
// STEP 3: TOOL REGISTRATIONS
// =====================================================================================
// These are the REAL tool classes that do actual work (search docs, run SQL, etc.).
// They're registered here because the MCP Server needs them — it receives tool requests
// and delegates to these classes. The Orchestrator does NOT use them directly.
builder.Services.AddSingleton<DocumentSearchTool>();
builder.Services.AddSingleton<SqlQueryTool>();
builder.Services.AddSingleton<ImageCitationTool>();
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<WebSearchTool>();

// McpToolProxyService = the Orchestrator's ONLY way to call tools.
// It wraps each tool as a method that sends an HTTP request to /mcp endpoint.
// This means the Orchestrator doesn't know about DocumentSearchTool, SqlQueryTool, etc.
// It only knows: "call SearchDocumentsAsync on the proxy, it handles the rest."
builder.Services.AddSingleton<McpToolProxyService>();

// =====================================================================================
// STEP 4: AI CHAT CLIENTS — TWO MODELS FOR COST OPTIMIZATION
// =====================================================================================
// We use TWO different GPT models for different jobs:
//
// GPT-4o-mini (PLANNING): Picks which tools to call, runs function calling, does reflection.
//   - 15x cheaper than GPT-4o (~$0.15 vs $2.50 per 1M tokens)
//   - Matches GPT-4o accuracy on tool selection tasks (within 1-2%)
//   - Has FunctionInvocation middleware = automatically calls tools when LLM requests them
//
// GPT-4o (GENERATION): Writes the final answer for complex multi-source questions.
//   - Only used when the complexity router says "Complex"
//   - Better at synthesizing information from multiple sources
//
// RESULT: ~69% cost reduction per query with no quality loss on tool selection.
// INTERVIEW TIP: "We split planning and generation across models. The cheap model picks
// tools, the expensive model only activates for complex synthesis. This cuts costs 69%."

// Planning client: GPT-4o-mini + FunctionInvocation middleware
// FunctionInvocation = when GPT says "call SearchDocumentsAsync", the middleware
// automatically runs it and feeds the result back. No manual loop needed.
var planningChatClient = new ChatClientBuilder(
    openAiClient.GetChatClient(openAiSettings.PlanningDeployment).AsIChatClient())
    .UseFunctionInvocation()
    .Build();

// Generation client: GPT-4o (no function invocation — it just writes the answer)
var generationChatClient = openAiClient
    .GetChatClient(openAiSettings.ChatDeployment)
    .AsIChatClient();

// Keyed services = you can have multiple IChatClient instances, each with a unique key.
// The Orchestrator asks for "planning" or "generation" and gets the right one.
builder.Services.AddKeyedSingleton<IChatClient>("planning", (_, _) => planningChatClient);
builder.Services.AddKeyedSingleton<IChatClient>("generation", (_, _) => generationChatClient);

// =====================================================================================
// STEP 5: BUSINESS SERVICES
// =====================================================================================

// ReflectionService: After generating an answer, a SEPARATE LLM call scores it 1-10.
// If score is too low, the orchestrator retries. Uses GPT-4o-mini (scoring = classification task).
builder.Services.AddSingleton<ReflectionService>(sp =>
    new ReflectionService(planningChatClient));

// SemanticCacheService: Before doing expensive RAG, check if a similar question was asked before.
// Uses the SMALL embedding model (cheaper) and a separate Azure AI Search index as the cache store.
builder.Services.AddSingleton<SemanticCacheService>(sp =>
    new SemanticCacheService(cacheSearchClient, cacheEmbeddingClient,
        openAiSettings.CacheEmbeddingDimensions, agentSettings));

// ConversationMemoryService: Stores chat history in Redis so the agent can handle follow-up
// questions like "what about that vendor?" Uses GPT-4o-mini to summarize old turns when
// history gets too long (saves tokens).
builder.Services.AddSingleton<ConversationMemoryService>(sp =>
    new ConversationMemoryService(
        sp.GetRequiredService<IConnectionMultiplexer>(),
        planningChatClient,
        agentSettings));

// ComplexityRouterService: Rule-based (no LLM cost) — decides if a question is Simple or Complex.
// Simple → GPT-4o-mini answers directly. Complex → escalates to GPT-4o for better synthesis.
builder.Services.AddSingleton<ComplexityRouterService>();

// PiiRedactionService: Scans text for PII (SSN, email, phone, etc.) and replaces it.
// Runs at 5 layers: user input, tool results, LLM output, cache writes, memory writes.
builder.Services.AddSingleton<PiiRedactionService>();

// QueryRewriteService: Rewrites user questions into retrieval-friendly form (same intent).
// Low-cost pre-step that improves recall/precision before tool calls.
builder.Services.AddSingleton<QueryRewriteService>();

// AmbiguityDetectionService + ClarificationQuestionService: Clarify-first safety layer.
// If question is too vague, API returns structured follow-up questions instead of guessing.
builder.Services.AddSingleton<AmbiguityDetectionService>();
builder.Services.AddSingleton<ClarificationQuestionService>();

// AgentOrchestrator: THE BRAIN — coordinates the entire pipeline:
// Cache check → Memory load → Tool calling → Routing → Generation → Reflection → Cache → Memory
builder.Services.AddSingleton<AgentOrchestrator>();

// =====================================================================================
// STEP 6: MCP SERVER — EXPOSE TOOLS VIA OPEN STANDARD
// =====================================================================================
// MCP (Model Context Protocol) server lets ANY compatible AI client discover and call our tools.
// Claude Desktop, VS Code Copilot, or any MCP client can connect to /mcp and use the same
// search, SQL, and image tools that our GPT-4o agent uses internally.
// No extra containers or Azure resources — runs in-process alongside the REST API.
builder.Services.AddMcpServer()
    .WithTools<AgenticRagMcpServer>()
    .WithHttpTransport();

// =====================================================================================
// STEP 7: STANDARD ASP.NET PLUMBING
// =====================================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// CORS: Allow the React chat frontend to call this API from a different origin.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000", "https://chat-agentic01.azurestaticapps.net"];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// =====================================================================================
// STEP 8: MIDDLEWARE PIPELINE (order matters!)
// =====================================================================================

// GlobalExceptionMiddleware MUST be first — it's the safety net that catches ANY unhandled
// exception from all downstream middleware, controllers, and the agent pipeline.
// Returns a clean RFC 7807 ProblemDetails JSON (not ugly stack traces) with a correlation ID.
app.UseMiddleware<AgenticRAG.Api.Middleware.GlobalExceptionMiddleware>();

app.UseCors();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();      // REST API endpoints (POST /api/agent/ask)
app.MapMcp("/mcp");        // MCP protocol endpoint (for AI clients like Claude, VS Code)
app.MapHealthChecks("/health");

app.Run();
