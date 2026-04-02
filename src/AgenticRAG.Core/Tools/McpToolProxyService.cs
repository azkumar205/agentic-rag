// =====================================================================================
// McpToolProxyService — UNIFIED MCP CLIENT: Routes ALL tool calls through MCP protocol
// =====================================================================================
//
// WHAT IS THIS?
// Instead of calling tool classes directly (DocumentSearchTool, SqlQueryTool, etc.),
// the orchestrator calls this proxy service. This proxy connects to the MCP Server
// via HTTP and invokes tools through the MCP (Model Context Protocol) standard.
//
// ARCHITECTURE SHIFT:
//   BEFORE (direct):  GPT-4o → FunctionInvocation → DocumentSearchTool.SearchDocumentsAsync()
//   AFTER (MCP):      GPT-4o → FunctionInvocation → McpToolProxyService → HTTP /mcp →
//                     AgenticRagMcpServer → DocumentSearchTool.SearchDocumentsAsync()
//
// WHY ROUTE EVERYTHING THROUGH MCP? (3 key benefits)
//   1. SINGLE PROTOCOL — all tools go through the same MCP standard. Any MCP client
//      (Claude, VS Code Copilot, Gemini) gets the same capabilities for free.
//   2. DECOUPLED — the orchestrator only knows MCP tool names, not concrete classes.
//      Tools can be swapped, versioned, or moved to a remote server without changing
//      the orchestrator code.
//   3. OBSERVABILITY — all tool calls flow through a single chokepoint, making it
//      easy to log, meter, and audit every invocation.
//
// HOW IT WORKS:
//   - 5 public methods (one per tool): SearchDocumentsAsync, QuerySqlAsync, etc.
//   - Each method is registered as an AIFunction tool via AIFunctionFactory.Create()
//   - GPT-4o's FunctionInvocationChatClient calls them automatically when it picks a tool
//   - Internally, each method calls CallMcpToolAsync() which sends an HTTP request to /mcp
//
// TRANSPORT: HttpClientTransport with AutoDetect mode
//   - Tries Streamable HTTP first (MCP 2025-03-26 spec)
//   - Falls back to SSE (MCP 2024-11-05 spec) if the server is older
//
// INTERVIEW TIP: "We use MCP as the tool abstraction layer — the orchestrator never
// calls tool classes directly. This means any MCP-compatible AI client can reuse our tools."
// =====================================================================================
using System.ComponentModel;
using System.Text;
using AgenticRAG.Core.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AgenticRAG.Core.Tools;

public class McpToolProxyService
{
    private readonly HttpClient _httpClient;      // Shared HTTP client for MCP connections
    private readonly McpProxySettings _settings;  // Contains the MCP endpoint URL

    public McpToolProxyService(HttpClient httpClient, McpProxySettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    // ── Tool 1: DOCUMENT SEARCH via MCP ──
    // Routes to MCP tool "search_documents" → AgenticRagMcpServer → DocumentSearchTool → Azure AI Search
    [Description("Search company documents (contracts, policies, reports, procedures). " +
                 "Returns relevant text passages with source document names.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query — be specific about what you're looking for")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["topK"] = topK
        };

        return await CallMcpToolAsync("search_documents", args, cancellationToken);
    }

    // ── Tool 2: SQL QUERY via MCP ──
    // Routes to MCP tool "query_sql" → AgenticRagMcpServer → SqlQueryTool → SQL Server
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

    // ── Tool 3: SCHEMA DISCOVERY via MCP ──
    // GPT-4o should call this FIRST before writing SQL to learn column names
    [Description("Get column names and types for available SQL views. Call this first if unsure about column names.")]
    public async Task<string> GetSchemaAsync(
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>();  // No arguments needed
        return await CallMcpToolAsync("get_schema", args, cancellationToken);
    }

    // ── Tool 4: DOCUMENT IMAGES via MCP ──
    // Routes to MCP tool "get_document_images" → AgenticRagMcpServer → ImageCitationTool → Blob Storage SAS URLs
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

    // ── Tool 5: WEB SEARCH via MCP ──
    // Routes to MCP tool "search_web" → AgenticRagMcpServer → WebSearchTool → Google Custom Search API
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

    // =====================================================================================
    // CallMcpToolAsync — THE CORE METHOD that every tool proxy method delegates to
    // =====================================================================================
    //
    // Creates an MCP client connection → invokes the named tool → parses response → returns text
    //
    // CONNECTION LIFECYCLE: Each call creates a fresh McpClient → calls tool → disposes.
    // This is simple and safe. For high-throughput scenarios, you'd cache the McpClient.
    //
    // MCP RESPONSE FORMAT: Tools return a list of "content blocks" (text, image, resource).
    // We concatenate all TextContentBlocks into a single string for GPT-4o to consume.
    // =====================================================================================
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
            // Step 1: Create HTTP transport pointing to the MCP server endpoint
            // AutoDetect tries Streamable HTTP first, falls back to SSE for older servers
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = endpointUri,
                    TransportMode = HttpTransportMode.AutoDetect
                },
                _httpClient,
                ownsHttpClient: false);   // Don't dispose the shared HttpClient — it's managed by DI

            // Step 2: Create MCP client — this performs the MCP initialization handshake
            await using var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions(),
                loggerFactory: null,
                cancellationToken: cancellationToken);

            // Step 3: Invoke the MCP tool by name with the argument dictionary
            // The MCP server deserializes args, calls the real tool, returns content blocks
            var result = await client.CallToolAsync(
                toolName,
                args,
                null,              // No progress reporting needed
                null,              // No custom request options
                cancellationToken);

            // Step 4: Check for MCP-level errors (tool returned isError = true)
            if (result.IsError == true)
            {
                return $"[MCP Error] Tool '{toolName}' returned an error.";
            }

            // Step 5: No content = tool ran but produced nothing
            if (result.Content == null || result.Content.Count == 0)
            {
                return $"[MCP] Tool '{toolName}' returned no content.";
            }

            // Step 6: Parse content blocks into a single string for GPT-4o
            // MCP tools return a list of content blocks (text, image, resource)
            // We concatenate all TextContentBlocks for the LLM to consume
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
                    // Non-text blocks (images, resources) — include as fallback string
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
            // Return error as text for GPT-4o to read — don't crash the agent loop
            return $"[MCP Error] Call to '{toolName}' failed: {ex.Message}";
        }
    }
}
