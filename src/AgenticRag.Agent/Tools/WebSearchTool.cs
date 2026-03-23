using System.Net.Http.Json;
using System.Text.Json;
using AgenticRag.Agent.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Agent.Tools;

/// <summary>
/// Agent tool that performs Bing web searches via the Azure Bing Search API.
/// </summary>
public class WebSearchTool : IAgentTool
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebSearchTool> _logger;

    public string Name => "web_search";
    public string Description => "Search the public web for current information. Input: plain text search query.";

    public WebSearchTool(IHttpClientFactory httpClientFactory, ILogger<WebSearchTool> logger)
    {
        _httpClient = httpClientFactory.CreateClient("BingSearch");
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[WebSearchTool] Searching web for: {Input}", input);

        var encoded = Uri.EscapeDataString(input);
        var response = await _httpClient.GetAsync(
            $"https://api.bing.microsoft.com/v7.0/search?q={encoded}&count=5",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[WebSearchTool] Bing search returned {StatusCode}", response.StatusCode);
            return JsonSerializer.Serialize(new { error = $"Web search failed: {response.StatusCode}" });
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var results = doc.RootElement
            .GetProperty("webPages")
            .GetProperty("value")
            .EnumerateArray()
            .Select(r => new
            {
                name = r.GetProperty("name").GetString(),
                url = r.GetProperty("url").GetString(),
                snippet = r.GetProperty("snippet").GetString()
            })
            .ToList();

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
    }
}
