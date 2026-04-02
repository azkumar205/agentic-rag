// =====================================================================================
// WebSearchTool — AI TOOL: Searches the public internet via Google Custom Search API
// =====================================================================================
//
// WHAT IS THIS?
// When the agent needs information that's NOT in internal documents or SQL (latest news,
// public references, external facts), it calls this tool to search the internet.
// Results are formatted as "[WebSource N]" for GPT-4o to cite in its answer.
//
// HOW IT WORKS:
//   1. GPT-4o decides it needs web results (e.g., "What's the latest pricing for Azure?")
//   2. Calls SearchWebAsync with the query
//   3. This tool calls Google Custom Search API
//   4. Returns title + URL + snippet for each result
//
// CONFIGURATION: Requires GoogleWebSearch:ApiKey and GoogleWebSearch:SearchEngineId
// in appsettings.json. If not configured, returns a helpful error message.
//
// INTERVIEW TIP: "Our agent can search the internet too — not just internal docs.
// This is useful for questions about current events or public references."
// =====================================================================================
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgenticRAG.Core.Configuration;

namespace AgenticRAG.Core.Tools;

public class WebSearchTool
{
    private readonly HttpClient _httpClient;              // Shared HTTP client for API calls
    private readonly GoogleWebSearchSettings _settings;   // API key, engine ID, endpoint URL

    public WebSearchTool(HttpClient httpClient, GoogleWebSearchSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    // GPT-4o reads this [Description] to decide when to call this tool
    [Description("Search the public internet using Google Custom Search API. " +
                 "Use this for latest events, public references, or external facts not present in internal sources.")]
    public async Task<string> SearchWebAsync(
        [Description("Web search query")]
        string query,
        [Description("Number of results to return (default 5, max 10)")]
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Check if web search is properly configured
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.SearchEngineId))
        {
            return "[WebSource] Web search is not configured. Set GoogleWebSearch:ApiKey and GoogleWebSearch:SearchEngineId.";
        }

        topK = Math.Clamp(topK, 1, 10);

        // URL-encode all parameters to prevent injection
        var encodedQuery = UrlEncoder.Default.Encode(query);
        var encodedEngineId = UrlEncoder.Default.Encode(_settings.SearchEngineId);
        var encodedApiKey = UrlEncoder.Default.Encode(_settings.ApiKey);

        var requestUrl =
            $"{_settings.Endpoint}?key={encodedApiKey}&cx={encodedEngineId}&q={encodedQuery}&num={topK}";

        try
        {
            // Call Google Custom Search API
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return $"[WebSource] Web search failed ({(int)response.StatusCode}): {content}";
            }

            // Parse the JSON response and extract search results
            using var jsonDoc = JsonDocument.Parse(content);
            if (!jsonDoc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return "[WebSource] No web results found.";
            }

            // Format each result as "[WebSource N]" with title, URL, and snippet
            var results = new List<string>();
            var index = 1;

            foreach (var item in items.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Untitled" : "Untitled";
                var link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? "" : "";
                var snippet = item.TryGetProperty("snippet", out var snippetProp) ? snippetProp.GetString() ?? "" : "";

                results.Add($"[WebSource {index}] {title}\nURL: {link}\nSnippet: {snippet}");
                index++;
            }

            return results.Count > 0
                ? string.Join("\n\n---\n\n", results)
                : "[WebSource] No web results found.";
        }
        catch (Exception ex)
        {
            return $"[WebSource] Web search error: {ex.Message}";
        }
    }
}
