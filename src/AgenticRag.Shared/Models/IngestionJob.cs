namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents a document ingestion job for tracking weekly incremental processing.
/// </summary>
public class IngestionJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public IngestionStatus Status { get; set; } = IngestionStatus.Pending;
    public string ContainerName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int DocumentsProcessed { get; set; }
    public int ChunksIndexed { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTimeOffset? LastIncrementalRunAt { get; set; }
}
