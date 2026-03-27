// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Program.cs — Composition root for the Agentic RAG API (MCP-Only).
//
// Architecture change: ALL tool calls now route through MCP.
//   BEFORE: ChatOptions held AIFunctions wrapping direct tool classes →
//           GPT-4o → FunctionInvocation → DocumentSearchTool (direct)
//   NOW:    Orchestrator builds ChatOptions from McpToolProxyService methods →
//           GPT-4o → FunctionInvocation → McpToolProxyService → HTTP /mcp →
//           AgenticRagMcpServer → actual tool classes
//
// ChatOptions is no longer registered as a DI singleton — the orchestrator
// builds it internally using McpToolProxyService method references.
//
// Key registrations:
//   • Azure clients: OpenAI, AI Search, Blob Storage, Redis
//   • AI Tools: DocumentSearchTool, SqlQueryTool, ImageCitationTool (for MCP server)
//   • McpToolProxyService: MCP client proxy that all orchestrator tool calls use
//   • IChatClient: GPT-4o with FunctionInvocation middleware
//   • MCP Server: Exposes tools via /mcp for both internal proxy and external clients
//   • Services: Cache, Memory, Reflection, Orchestrator
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Storage.Blobs;
using AgenticRAG.Core.Agents;
using AgenticRAG.Core.Caching;
using AgenticRAG.Core.Configuration;
using AgenticRAG.Core.McpTools;
using AgenticRAG.Core.Memory;
using AgenticRAG.Core.Tools;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ──────────── Configuration Binding ────────────
// Each section maps to a strongly-typed settings class (see AgenticRagSettings.cs)
var searchSettings = builder.Configuration.GetSection("AzureAISearch").Get<AzureAISearchSettings>()!;
var openAiSettings = builder.Configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>()!;
var sqlSettings = builder.Configuration.GetSection("SqlServer").Get<SqlServerSettings>()!;
var blobSettings = builder.Configuration.GetSection("BlobStorage").Get<BlobStorageSettings>()!;
var redisSettings = builder.Configuration.GetSection("Redis").Get<RedisSettings>()!;
var agentSettings = builder.Configuration.GetSection("Agent").Get<AgentSettings>()!;
var webSearchSettings = builder.Configuration.GetSection("GoogleWebSearch").Get<GoogleWebSearchSettings>()!;
var mcpProxySettings = builder.Configuration.GetSection("McpProxy").Get<McpProxySettings>()!;

builder.Services.AddSingleton(searchSettings);
builder.Services.AddSingleton(openAiSettings);
builder.Services.AddSingleton(sqlSettings);
builder.Services.AddSingleton(blobSettings);
builder.Services.AddSingleton(redisSettings);
builder.Services.AddSingleton(agentSettings);
builder.Services.AddSingleton(webSearchSettings);
builder.Services.AddSingleton(mcpProxySettings);

// ──────────── Azure Clients ────────────
// DefaultAzureCredential: works with Managed Identity on Azure, and your VS/CLI login locally
var credential = new DefaultAzureCredential();

var openAiClient = new AzureOpenAIClient(new Uri(openAiSettings.Endpoint), credential);
builder.Services.AddSingleton(openAiClient);

var searchClient = new SearchClient(
    new Uri(searchSettings.Endpoint),
    searchSettings.IndexName,
    credential);
builder.Services.AddSingleton(searchClient);

var cacheSearchClient = new SearchClient(
    new Uri(searchSettings.Endpoint),
    "semantic-cache",
    credential);

// ── Dual Embedding Clients (Multi-Embedding Cost Optimization) ──
// Document search uses text-embedding-3-large (1536d) — high quality, same model as ingestion
// Semantic cache uses text-embedding-3-small (512d) — cheaper, question-to-question only
var docEmbeddingClient = openAiClient.GetEmbeddingClient(openAiSettings.EmbeddingDeployment);
builder.Services.AddSingleton(docEmbeddingClient);  // Used by DocumentSearchTool via MCP

var cacheEmbeddingClient = openAiClient.GetEmbeddingClient(openAiSettings.CacheEmbeddingDeployment);

var blobServiceClient = new BlobServiceClient(
    new Uri($"https://{blobSettings.AccountName}.blob.core.windows.net"), credential);
builder.Services.AddSingleton(blobServiceClient);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisSettings.ConnectionString));

// ──────────── Tool Registrations ────────────
// These tool classes are still registered because the MCP *server* needs them.
// AgenticRagMcpServer receives DI-injected tool instances to execute real work.
// However, the orchestrator does NOT reference these directly — it goes through MCP.
builder.Services.AddSingleton<DocumentSearchTool>();
builder.Services.AddSingleton<SqlQueryTool>();
builder.Services.AddSingleton<ImageCitationTool>();
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<WebSearchTool>();

// McpToolProxyService — the orchestrator's ONLY tool dependency.
// Wraps all 5 tools as methods that route through MCP protocol to /mcp endpoint.
// Replaces both direct tool calls AND the old McpWebSearchProxyTool.
builder.Services.AddSingleton<McpToolProxyService>();

// ──────────── AI Chat Clients — Multi-Model Cost Optimization ────────────
// Two chat clients for different roles:
//   PLANNING (GPT-4o-mini): Tool selection, function calling, reflection, summarization
//     — 15x cheaper than GPT-4o, matches accuracy on classification/tool-calling tasks
//   GENERATION (GPT-4o): Complex multi-source synthesis when router decides "complex"
//     — Full quality preserved for analytical, comparative, multi-document answers

// Planning client — GPT-4o-mini with FunctionInvocation middleware (handles tool calls)
var planningChatClient = new ChatClientBuilder(
    openAiClient.GetChatClient(openAiSettings.PlanningDeployment).AsIChatClient())
    .UseFunctionInvocation()
    .Build();

// Generation client — GPT-4o for complex answers (no function invocation needed)
var generationChatClient = openAiClient
    .GetChatClient(openAiSettings.ChatDeployment)
    .AsIChatClient();

builder.Services.AddKeyedSingleton<IChatClient>("planning", (_, _) => planningChatClient);
builder.Services.AddKeyedSingleton<IChatClient>("generation", (_, _) => generationChatClient);

// ── No ChatOptions singleton needed ──
// The orchestrator now builds ChatOptions internally using McpToolProxyService
// methods. All tool calls route through MCP protocol to /mcp endpoint.
// See AgentOrchestrator.BuildMcpChatOptions() for the tool registration.

// ──────────── Services ────────────
// ReflectionService uses planning client (GPT-4o-mini) — scoring is a classification task
builder.Services.AddSingleton<ReflectionService>(sp =>
    new ReflectionService(planningChatClient));

// SemanticCacheService uses the SMALL embedding client (text-embedding-3-small, 512d)
// Different model from document search is safe here — cache index is independent
builder.Services.AddSingleton<SemanticCacheService>(sp =>
    new SemanticCacheService(cacheSearchClient, cacheEmbeddingClient,
        openAiSettings.CacheEmbeddingDimensions, agentSettings));

// ConversationMemoryService uses planning client (GPT-4o-mini) for summarization
builder.Services.AddSingleton<ConversationMemoryService>(sp =>
    new ConversationMemoryService(
        sp.GetRequiredService<IConnectionMultiplexer>(),
        planningChatClient,
        agentSettings));

builder.Services.AddSingleton<ComplexityRouterService>();
builder.Services.AddSingleton<AgentOrchestrator>();

// ──────────── MCP Server ────────────
// Exposes tools via Model Context Protocol — any MCP-compatible client
// (Claude, Gemini, VS Code, etc.) can discover and call these tools.
builder.Services.AddMcpServer()
    .WithTools<AgenticRagMcpServer>()
    .WithHttpTransport();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

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

app.UseCors();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();
app.MapMcp("/mcp");              // MCP protocol endpoint
app.MapHealthChecks("/health");

app.Run();
