// =====================================================================================
// AgenticRagMcpServer — MCP SERVER: Exposes all AI tools via the MCP protocol
// =====================================================================================
//
// WHAT IS THIS?
// This class wraps the same 5 tools that GPT-4o uses internally, but exposes them
// as an MCP (Model Context Protocol) Server. This means ANY MCP-compatible AI client
// (Claude, Gemini, VS Code Copilot, etc.) can discover and call these tools.
//
// WITHOUT MCP: Tools are only callable by GPT-4o via AIFunctionFactory.Create()
// WITH MCP:    Any AI client can call them via the standard MCP protocol — no code changes
//
// HOW IT WORKS:
//   1. Each method has [McpServerTool] — registers it in the MCP tool discovery response
//   2. [Description] attributes tell MCP clients what the tool does and its parameters
//   3. Each method simply delegates to the existing tool class (NO logic duplication)
//   4. Program.cs wires this up: builder.Services.AddMcpServer().WithTools<AgenticRagMcpServer>()
//   5. Endpoint is mapped: app.MapMcp("/mcp")
//
// ARCHITECTURE (all in-process, no extra containers):
//   HTTP request → /mcp endpoint → MCP protocol handler → AgenticRagMcpServer methods
//   → existing Tool classes (DocumentSearchTool, SqlQueryTool, etc.)
//
// ReadOnly = true: Tells MCP clients these tools only READ data, never modify state.
// This makes them safe for automated/batch invocations without human approval.
//
// INTERVIEW TIP: "We run an MCP server in-process — same .NET process as the API.
// This lets any MCP client (not just GPT-4o) discover and call our tools."
// =====================================================================================
using System.ComponentModel;
using AgenticRAG.Core.Tools;
using ModelContextProtocol.Server;

namespace AgenticRAG.Core.McpTools;

public class AgenticRagMcpServer
{
    private readonly DocumentSearchTool _searchTool;    // Azure AI Search (hybrid + semantic reranking)
    private readonly SqlQueryTool _sqlTool;              // SQL Server (SELECT-only, whitelisted views)
    private readonly ImageCitationTool _imageTool;       // Blob Storage (image SAS URLs)
    private readonly WebSearchTool _webSearchTool;       // Google Custom Search API

    // Same tool instances used by BOTH the AIFunctionFactory (GPT-4o) path AND this MCP server path
    public AgenticRagMcpServer(
        DocumentSearchTool searchTool,
        SqlQueryTool sqlTool,
        ImageCitationTool imageTool,
        WebSearchTool webSearchTool)
    {
        _searchTool = searchTool;
        _sqlTool = sqlTool;
        _imageTool = imageTool;
        _webSearchTool = webSearchTool;
    }

    // ── MCP Tool 1: Document Search ──
    // Delegates to DocumentSearchTool → Azure AI Search (BM25 + vector + semantic rerank)
    [McpServerTool(Name = "search_documents", ReadOnly = true),
     Description("Search company documents (contracts, policies, reports, procedures). " +
                 "Returns relevant text passages with source document names.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query — be specific about what you're looking for")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5)
        => await _searchTool.SearchDocumentsAsync(query, topK);

    // ── MCP Tool 2: SQL Query ──
    // Delegates to SqlQueryTool → validates query (SELECT only, whitelisted views) → executes
    [McpServerTool(Name = "query_sql", ReadOnly = true),
     Description("Query structured business data from SQL Server using SELECT statements. " +
                 "Available views: vw_BillingOverview, vw_ContractSummary, vw_InvoiceDetail, vw_VendorAnalysis.")]
    public async Task<string> QuerySqlAsync(
        [Description("A SELECT SQL query using ONLY the allowed views")] string sqlQuery)
        => await _sqlTool.QuerySqlAsync(sqlQuery);

    // ── MCP Tool 3: Schema Discovery ──
    // Returns column names/types so MCP clients can write correct SQL queries
    [McpServerTool(Name = "get_schema", ReadOnly = true),
     Description("Get column names and types for available SQL views. Call this first if unsure about column names.")]
    public async Task<string> GetSchemaAsync()
        => await _sqlTool.GetSchemaAsync();

    // ── MCP Tool 4: Document Images ──
    // Delegates to ImageCitationTool → Blob Storage → generates time-limited SAS download URLs
    [McpServerTool(Name = "get_document_images", ReadOnly = true),
     Description("Get downloadable image URLs (charts, diagrams, tables) from a document.")]
    public async Task<string> GetDocumentImagesAsync(
        [Description("Document filename (e.g., 'acme-contract.pdf')")] string documentName,
        [Description("Optional: specific page number to get images from")] int? pageNumber = null)
        => await _imageTool.GetDocumentImagesAsync(documentName, pageNumber);

    // ── MCP Tool 5: Web Search ──
    // Delegates to WebSearchTool → Google Custom Search API
    [McpServerTool(Name = "search_web", ReadOnly = true),
     Description("Search the public internet for latest/public information using Google web search.")]
    public async Task<string> SearchWebAsync(
        [Description("Web search query")]
        string query,
        [Description("Number of results to return (default 5, max 10)")]
        int topK = 5,
        CancellationToken cancellationToken = default)
        => await _webSearchTool.SearchWebAsync(query, topK, cancellationToken);
}
