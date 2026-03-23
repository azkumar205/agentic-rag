using System.Text.Json;
using AgenticRag.Agent.Interfaces;
using AgenticRag.DataAccess.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Agent.Tools;

/// <summary>
/// Agent tool that performs semantic and hybrid document search over Azure AI Search.
/// </summary>
public class DocumentSearchTool : IAgentTool
{
    private readonly ISearchService _searchService;
    private readonly ILogger<DocumentSearchTool> _logger;

    public string Name => "document_search";
    public string Description => "Search indexed documents using semantic or hybrid search. Input: plain text search query.";

    public DocumentSearchTool(ISearchService searchService, ILogger<DocumentSearchTool> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DocumentSearchTool] Query: {Input}", input);

        var results = await _searchService.HybridSearchAsync(input, topK: 5, cancellationToken);

        var output = results.Select(r => new
        {
            r.FileName,
            r.PageNumber,
            r.Section,
            r.Content,
            r.ContentType
        });

        return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = false });
    }
}
