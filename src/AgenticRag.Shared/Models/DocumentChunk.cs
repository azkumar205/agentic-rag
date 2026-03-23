namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents a chunk of a document that has been indexed for search.
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ContentType ContentType { get; set; }
    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; }
    public string Section { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public DateTimeOffset IngestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
