// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// AgenticRagMcpServer — MCP (Model Context Protocol) Server for AgenticRAG.
//
// This class exposes the same 4 AI tools that GPT-4o uses internally,
// but wrapped as an MCP Server instead of AIFunctionFactory registrations.
//
// Why MCP?
//   Without MCP: Tools are registered via AIFunctionFactory.Create() and
//   only GPT-4o (via FunctionInvocationChatClient) can call them.
//   With MCP: Any MCP-compatible AI client (Claude, Gemini, VS Code,
//   Copilot, etc.) can discover and invoke these same tools via the
//   standard MCP protocol — no code changes needed per model.
//
// How it works:
//   1. Each method is decorated with [McpServerTool] — this registers
//      it as a tool in the MCP protocol's tool discovery response.
//   2. [Description] attributes provide the tool/parameter descriptions
//      that MCP clients see (same role as for GPT-4o tool selection).
//   3. Each method delegates to the existing tool class (no logic duplication).
//   4. Program.cs wires this up via: builder.Services.AddMcpServer()
//      .WithTools<AgenticRagMcpServer>().WithHttpTransport();
//   5. The endpoint is mapped via: app.MapMcp("/mcp");
//
// Architecture (in-process):
//   HTTP request → /mcp endpoint → MCP protocol handler
//       → AgenticRagMcpServer methods → existing Tool classes
//   No separate containers, no extra Azure resources — runs in the
//   same .NET process alongside the REST API.
//
// ReadOnly = true: Tells MCP clients these tools only read data,
// never modify state — safe for automated/batch invocations.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.ComponentModel;
using AgenticRAG.Core.Tools;
using ModelContextProtocol.Server;

namespace AgenticRAG.Core.McpTools;

public class AgenticRagMcpServer
{
    private readonly DocumentSearchTool _searchTool;   // Azure AI Search (hybrid + semantic)
    private readonly SqlQueryTool _sqlTool;             // SQL Server read-only views
    private readonly ImageCitationTool _imageTool;      // Blob Storage image SAS URLs
    private readonly WebSearchTool _webSearchTool;      // Internet search via Google API

    // Constructor injection — same tool instances used by both the
    // AIFunctionFactory (GPT-4o) path and this MCP server path.
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

    // ── Tool 1: Document Search ──
    // Delegates to DocumentSearchTool.SearchDocumentsAsync which runs
    // hybrid search (BM25 + vector + semantic rerank) against Azure AI Search.
    [McpServerTool(Name = "search_documents", ReadOnly = true),
     Description("Search company documents (contracts, policies, reports, procedures). " +
                 "Returns relevant text passages with source document names.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query — be specific about what you're looking for")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5)
        => await _searchTool.SearchDocumentsAsync(query, topK);

    // ── Tool 2: SQL Query ──
    // Delegates to SqlQueryTool.QuerySqlAsync which validates the query
    // (SELECT only, whitelisted views) and executes against SQL Server.
    [McpServerTool(Name = "query_sql", ReadOnly = true),
     Description("Query structured business data from SQL Server using SELECT statements. " +
                 "Available views: vw_BillingOverview, vw_ContractSummary, vw_InvoiceDetail, vw_VendorAnalysis.")]
    public async Task<string> QuerySqlAsync(
        [Description("A SELECT SQL query using ONLY the allowed views")] string sqlQuery)
        => await _sqlTool.QuerySqlAsync(sqlQuery);

    // ── Tool 3: Schema Discovery ──
    // Returns column names and types for all available SQL views.
    // MCP clients should call this first to learn the schema before writing SQL.
    [McpServerTool(Name = "get_schema", ReadOnly = true),
     Description("Get column names and types for available SQL views. Call this first if unsure about column names.")]
    public async Task<string> GetSchemaAsync()
        => await _sqlTool.GetSchemaAsync();

    // ── Tool 4: Document Images ──
    // Delegates to ImageCitationTool which lists matching blobs in the
    // "images" container and generates time-limited SAS download URLs.
    [McpServerTool(Name = "get_document_images", ReadOnly = true),
     Description("Get downloadable image URLs (charts, diagrams, tables) from a document.")]
    public async Task<string> GetDocumentImagesAsync(
        [Description("Document filename (e.g., 'acme-contract.pdf')")] string documentName,
        [Description("Optional: specific page number to get images from")] int? pageNumber = null)
        => await _imageTool.GetDocumentImagesAsync(documentName, pageNumber);

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
