using AgenticRag.Shared.Models;

namespace AgenticRag.DataAccess.Interfaces;

/// <summary>
/// Interface for document ingestion orchestration.
/// </summary>
public interface IDocumentIngestionService
{
    Task<IngestionJob> RunIncrementalIngestionAsync(string containerName, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
    Task<IngestionJob> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default);
}
