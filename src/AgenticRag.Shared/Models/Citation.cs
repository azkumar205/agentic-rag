namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents a citation from a retrieved document chunk.
/// </summary>
public class Citation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Section { get; set; } = string.Empty;
    public ContentType ContentType { get; set; }
    public double RelevanceScore { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
}
