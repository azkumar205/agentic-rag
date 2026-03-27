using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgenticRAG.Core.Configuration;

namespace AgenticRAG.Core.Tools;

public class WebSearchTool
{
    private readonly HttpClient _httpClient;
    private readonly GoogleWebSearchSettings _settings;

    public WebSearchTool(HttpClient httpClient, GoogleWebSearchSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    [Description("Search the public internet using Google Custom Search API. " +
                 "Use this for latest events, public references, or external facts not present in internal sources.")]
    public async Task<string> SearchWebAsync(
        [Description("Web search query")]
        string query,
        [Description("Number of results to return (default 5, max 10)")]
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.SearchEngineId))
        {
            return "[WebSource] Web search is not configured. Set GoogleWebSearch:ApiKey and GoogleWebSearch:SearchEngineId.";
        }

        topK = Math.Clamp(topK, 1, 10);

        var encodedQuery = UrlEncoder.Default.Encode(query);
        var encodedEngineId = UrlEncoder.Default.Encode(_settings.SearchEngineId);
        var encodedApiKey = UrlEncoder.Default.Encode(_settings.ApiKey);

        var requestUrl =
            $"{_settings.Endpoint}?key={encodedApiKey}&cx={encodedEngineId}&q={encodedQuery}&num={topK}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return $"[WebSource] Web search failed ({(int)response.StatusCode}): {content}";
            }

            using var jsonDoc = JsonDocument.Parse(content);
            if (!jsonDoc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return "[WebSource] No web results found.";
            }

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
