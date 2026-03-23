namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents the result of a RAG search query.
/// </summary>
public class SearchResult
{
    public string Answer { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public SearchType SearchType { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool FromCache { get; set; }
}
