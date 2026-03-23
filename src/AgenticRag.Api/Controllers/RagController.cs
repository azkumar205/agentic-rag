using Microsoft.AspNetCore.Mvc;
using AgenticRag.DataAccess.Interfaces;
using AgenticRag.Shared.Models;
using AgenticRag.Api.Models;

namespace AgenticRag.Api.Controllers;

/// <summary>
/// Classic RAG controller: semantic and hybrid search with downloadable citations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ILogger<RagController> _logger;

    public RagController(ISearchService searchService, ILogger<RagController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Performs a semantic or hybrid search and returns grounded results with citations.
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(SearchResult), 200)]
    public async Task<IActionResult> Query([FromBody] RagQueryRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RAG query received: {Query}", request.Query);

        IEnumerable<DocumentChunk> chunks;

        if (string.Equals(request.SearchType, "Semantic", StringComparison.OrdinalIgnoreCase))
        {
            chunks = await _searchService.SemanticSearchAsync(request.Query, request.TopK, cancellationToken);
        }
        else
        {
            chunks = await _searchService.HybridSearchAsync(request.Query, request.TopK, cancellationToken);
        }

        var citations = chunks.Select((c, i) => new Citation
        {
            Id = c.Id,
            DocumentId = c.DocumentId,
            FileName = c.FileName,
            Snippet = c.Content.Length > 300 ? c.Content[..300] + "..." : c.Content,
            PageNumber = c.PageNumber,
            Section = c.Section,
            ContentType = c.ContentType,
            RelevanceScore = 1.0 - (i * 0.1)
        }).ToList();

        var result = new SearchResult
        {
            Query = request.Query,
            Answer = citations.Count > 0
                ? $"Found {citations.Count} relevant sections. See citations for details."
                : "No relevant documents found for your query.",
            Citations = citations,
            SearchType = Enum.TryParse<SearchType>(request.SearchType, out var st) ? st : SearchType.Hybrid
        };

        return Ok(result);
    }

    /// <summary>
    /// Downloads citations as a JSON bundle for the given search result.
    /// </summary>
    [HttpPost("citations/download")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public IActionResult DownloadCitations([FromBody] List<Citation> citations)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(citations,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"citations-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
    }
}
