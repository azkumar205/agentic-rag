// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// McpToolProxyService — Unified MCP client that routes ALL tool calls
// through the MCP server instead of calling tool classes directly.
//
// Architecture shift:
//   BEFORE (direct):   GPT-4o → FunctionInvocation → DocumentSearchTool.SearchDocumentsAsync()
//   AFTER  (MCP):      GPT-4o → FunctionInvocation → McpToolProxyService → HTTP /mcp →
//                      AgenticRagMcpServer → DocumentSearchTool.SearchDocumentsAsync()
//
// Why route everything through MCP?
//   1. Single protocol — all tools (internal + external) go through the
//      same MCP standard, so any MCP client gets the same capabilities.
//   2. Decoupled execution — the orchestrator doesn't depend on concrete
//      tool classes; it only knows MCP tool names. Tools can be swapped,
//      versioned, or moved to a remote server without touching the orchestrator.
//   3. Observability — all tool calls flow through a single chokepoint,
//      making it easy to log, meter, and audit every invocation.
//   4. Protocol consistency — external MCP clients (Claude, VS Code, etc.)
//      and the internal GPT-4o agent call the exact same MCP endpoint.
//
// How it works:
//   - Each public method wraps one MCP tool name (search_documents, query_sql, etc.)
//   - Methods are registered as AIFunction tools via AIFunctionFactory.Create() in
//     ChatOptions, so GPT-4o's FunctionInvocationChatClient calls them automatically.
//   - Inside each method, an McpClient connects to the /mcp HTTP endpoint,
//     calls the named tool, and returns the text result.
//   - The [Description] attributes match the MCP server's tool descriptions
//     so GPT-4o selects tools with the same intelligence as before.
//
// Transport: HttpClientTransport with AutoDetect mode
//   - Tries Streamable HTTP first (MCP 2025-03-26 spec)
//   - Falls back to SSE (MCP 2024-11-05 spec) if needed
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.ComponentModel;
using System.Text;
using AgenticRAG.Core.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AgenticRAG.Core.Tools;

public class McpToolProxyService
{
    private readonly HttpClient _httpClient;
    private readonly McpProxySettings _settings;

    public McpToolProxyService(HttpClient httpClient, McpProxySettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    // ── Tool 1: Document Search (via MCP) ──
    // Routes to MCP tool "search_documents" → AgenticRagMcpServer.SearchDocumentsAsync
    // → DocumentSearchTool → Azure AI Search (hybrid + semantic rerank).
    [Description("Search company documents (contracts, policies, reports, procedures). " +
                 "Returns relevant text passages with source document names.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query — be specific about what you're looking for")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Build MCP arguments dictionary matching the server's parameter names
        var args = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["topK"] = topK
        };

        return await CallMcpToolAsync("search_documents", args, cancellationToken);
    }

    // ── Tool 2: SQL Query (via MCP) ──
    // Routes to MCP tool "query_sql" → AgenticRagMcpServer.QuerySqlAsync
    // → SqlQueryTool → SQL Server (SELECT-only, whitelisted views).
    [Description("Query structured business data from SQL Server using SELECT statements. " +
                 "Available views: vw_BillingOverview, vw_ContractSummary, vw_InvoiceDetail, vw_VendorAnalysis.")]
    public async Task<string> QuerySqlAsync(
        [Description("A SELECT SQL query using ONLY the allowed views")] string sqlQuery,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["sqlQuery"] = sqlQuery
        };

        return await CallMcpToolAsync("query_sql", args, cancellationToken);
    }

    // ── Tool 3: Schema Discovery (via MCP) ──
    // Routes to MCP tool "get_schema" → AgenticRagMcpServer.GetSchemaAsync
    // → SqlQueryTool → returns column names/types for all SQL views.
    // GPT-4o should call this FIRST before writing any SQL query.
    [Description("Get column names and types for available SQL views. Call this first if unsure about column names.")]
    public async Task<string> GetSchemaAsync(
        CancellationToken cancellationToken = default)
    {
        // get_schema takes no arguments — empty dictionary
        var args = new Dictionary<string, object?>();

        return await CallMcpToolAsync("get_schema", args, cancellationToken);
    }

    // ── Tool 4: Document Images (via MCP) ──
    // Routes to MCP tool "get_document_images" → AgenticRagMcpServer.GetDocumentImagesAsync
    // → ImageCitationTool → Blob Storage (generates SAS download URLs).
    [Description("Get downloadable image URLs (charts, diagrams, tables) from a document.")]
    public async Task<string> GetDocumentImagesAsync(
        [Description("Document filename (e.g., 'acme-contract.pdf')")] string documentName,
        [Description("Optional: specific page number to get images from")] int? pageNumber = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["documentName"] = documentName,
            ["pageNumber"] = pageNumber
        };

        return await CallMcpToolAsync("get_document_images", args, cancellationToken);
    }

    // ── Tool 5: Web Search (via MCP) ──
    // Routes to MCP tool "search_web" → AgenticRagMcpServer.SearchWebAsync
    // → WebSearchTool → Google Custom Search API.
    [Description("Search the public internet for latest/public information using web search.")]
    public async Task<string> SearchWebAsync(
        [Description("Web search query")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["topK"] = topK
        };

        return await CallMcpToolAsync("search_web", args, cancellationToken);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // CallMcpToolAsync — Central method that every tool proxy delegates to.
    //
    // Creates an MCP client connection to the /mcp endpoint, invokes the
    // named tool with the given arguments, and parses the response content
    // blocks into a single string result for GPT-4o to consume.
    //
    // Connection lifecycle: Each call creates a fresh McpClient → calls tool
    // → disposes. This is simple and safe for per-request usage. For high-
    // throughput scenarios, consider caching the McpClient instance.
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private async Task<string> CallMcpToolAsync(
        string toolName,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        // Validate the MCP endpoint URL from configuration
        if (!Uri.TryCreate(_settings.Endpoint, UriKind.Absolute, out var endpointUri))
        {
            return $"[MCP Error] Endpoint is invalid. Set McpProxy:Endpoint to a valid URL (e.g., https://<api>/mcp).";
        }

        try
        {
            // Create an HTTP transport pointing to the MCP server's endpoint.
            // AutoDetect tries Streamable HTTP first, falls back to SSE.
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = endpointUri,
                    TransportMode = HttpTransportMode.AutoDetect
                },
                _httpClient,
                ownsHttpClient: false);   // Don't dispose the shared HttpClient

            // Create MCP client — performs protocol initialization handshake
            await using var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions(),
                loggerFactory: null,
                cancellationToken: cancellationToken);

            // Invoke the MCP tool by name with the argument dictionary.
            // The MCP server deserializes args, calls the real tool, returns content blocks.
            var result = await client.CallToolAsync(
                toolName,
                args,
                null,              // No progress reporting
                null,              // No custom request options
                cancellationToken);

            // Check for MCP-level errors (tool returned isError = true)
            if (result.IsError == true)
            {
                return $"[MCP Error] Tool '{toolName}' returned an error.";
            }

            // No content returned — tool executed but produced nothing
            if (result.Content == null || result.Content.Count == 0)
            {
                return $"[MCP] Tool '{toolName}' returned no content.";
            }

            // Parse content blocks into a single string.
            // MCP tools return a list of content blocks (text, image, resource).
            // We concatenate all TextContentBlocks for GPT-4o consumption.
            var sb = new StringBuilder();
            foreach (var block in result.Content)
            {
                if (block is TextContentBlock text && !string.IsNullOrWhiteSpace(text.Text))
                {
                    if (sb.Length > 0)
                        sb.AppendLine().AppendLine();

                    sb.Append(text.Text);
                }
                else
                {
                    // Non-text blocks (images, resources) — include ToString() fallback
                    if (sb.Length > 0)
                        sb.AppendLine().AppendLine();

                    sb.Append(block.ToString());
                }
            }

            return sb.Length > 0
                ? sb.ToString()
                : $"[MCP] Tool '{toolName}' returned empty text content.";
        }
        catch (Exception ex)
        {
            // Catch transport/protocol/network errors — return as text for GPT-4o
            // rather than crashing the agent loop
            return $"[MCP Error] Call to '{toolName}' failed: {ex.Message}";
        }
    }
}
