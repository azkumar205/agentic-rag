// =====================================================================================
// McpWebSearchProxyTool — LEGACY: Single-tool MCP proxy for web search only
// =====================================================================================
//
// WHAT IS THIS?
// This was the ORIGINAL MCP proxy that only handled web search. It has been
// SUPERSEDED by McpToolProxyService which handles ALL 5 tools through MCP.
//
// WHY DOES IT STILL EXIST?
// It's kept for backward compatibility — some deployments may still reference it.
// For new code, use McpToolProxyService.SearchWebAsync() instead.
//
// HOW IT WORKS: Same pattern as McpToolProxyService — creates an MCP client,
// calls the "search_web" tool, parses the response.
//
// INTERVIEW TIP: "We started with a single MCP proxy for web search, then
// refactored to a unified proxy (McpToolProxyService) when we added more MCP tools."
// =====================================================================================
using System.ComponentModel;
using System.Text;
using AgenticRAG.Core.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AgenticRAG.Core.Tools;

public class McpWebSearchProxyTool
{
    private readonly HttpClient _httpClient;
    private readonly McpProxySettings _settings;

    public McpWebSearchProxyTool(HttpClient httpClient, McpProxySettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    // Same pattern as McpToolProxyService but only for web search
    [Description("Search the public internet via MCP tool endpoint. " +
                 "This routes web search through MCP while other internal tools can remain direct.")]
    public async Task<string> SearchWebViaMcpAsync(
        [Description("Web search query")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_settings.Endpoint, UriKind.Absolute, out var endpointUri))
        {
            return "[WebSource] MCP proxy endpoint is invalid. Set McpProxy:Endpoint to a valid URL (e.g., https://<api>/mcp).";
        }

        topK = Math.Clamp(topK, 1, 10);

        try
        {
            // Create MCP HTTP transport (AutoDetect: Streamable HTTP → SSE fallback)
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = endpointUri,
                    TransportMode = HttpTransportMode.AutoDetect
                },
                _httpClient,
                ownsHttpClient: false);

            // Connect to MCP server
            await using var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions(),
                loggerFactory: null,
                cancellationToken: cancellationToken);

            // Call the "search_web" tool via MCP protocol
            var args = new Dictionary<string, object?>
            {
                ["query"] = query,
                ["topK"] = topK
            };

            var result = await client.CallToolAsync(
                "search_web",
                args,
                null,
                null,
                cancellationToken);

            if (result.IsError == true)
            {
                return "[WebSource] MCP tool returned an error.";
            }

            if (result.Content == null || result.Content.Count == 0)
            {
                return "[WebSource] MCP tool returned no content.";
            }

            // Parse text content blocks into a single string
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
                    if (sb.Length > 0)
                        sb.AppendLine().AppendLine();

                    sb.Append(block.ToString());
                }
            }

            return sb.Length > 0
                ? sb.ToString()
                : "[WebSource] MCP tool returned empty text content.";
        }
        catch (Exception ex)
        {
            return $"[WebSource] MCP proxy call failed: {ex.Message}";
        }
    }
}
