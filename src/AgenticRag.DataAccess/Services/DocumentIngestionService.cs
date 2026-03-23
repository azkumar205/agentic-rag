using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticRag.DataAccess.Services;

/// <summary>
/// Orchestrates weekly incremental document ingestion from Azure Blob Storage.
/// Downloads new/updated documents, extracts content, generates embeddings, and indexes to Azure AI Search.
/// </summary>
public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly IDocumentExtractionService _extractionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchService _searchService;
    private readonly AzureStorageOptions _storageOptions;
    private readonly ILogger<DocumentIngestionService> _logger;

    private readonly Dictionary<string, IngestionJob> _jobs = new();

    public DocumentIngestionService(
        IBlobStorageService blobStorageService,
        IDocumentExtractionService extractionService,
        IEmbeddingService embeddingService,
        ISearchService searchService,
        IOptions<AzureStorageOptions> storageOptions,
        ILogger<DocumentIngestionService> logger)
    {
        _blobStorageService = blobStorageService;
        _extractionService = extractionService;
        _embeddingService = embeddingService;
        _searchService = searchService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<IngestionJob> RunIncrementalIngestionAsync(
        string containerName,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var job = new IngestionJob
        {
            ContainerName = containerName,
            Status = IngestionStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            LastIncrementalRunAt = since
        };
        _jobs[job.Id] = job;

        _logger.LogInformation("Starting incremental ingestion for container '{Container}' since {Since}", containerName, since);

        try
        {
            var blobNames = await _blobStorageService.ListNewDocumentsAsync(since, containerName, cancellationToken);

            foreach (var blobName in blobNames)
            {
                try
                {
                    _logger.LogInformation("Processing document: {BlobName}", blobName);

                    using var stream = await _blobStorageService.DownloadDocumentAsync(containerName, blobName, cancellationToken);
                    var chunks = (await _extractionService.ExtractChunksAsync(stream, blobName, cancellationToken)).ToList();

                    // Generate embeddings in batch
                    var texts = chunks.Select(c => c.Content).ToList();
                    var embeddings = (await _embeddingService.GenerateBatchEmbeddingsAsync(texts, cancellationToken)).ToList();

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        chunks[i].Embedding = embeddings[i];
                    }

                    await _searchService.IndexDocumentChunksAsync(chunks, cancellationToken);

                    job.DocumentsProcessed++;
                    job.ChunksIndexed += chunks.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document '{BlobName}'", blobName);
                    job.Errors.Add($"Error processing '{blobName}': {ex.Message}");
                }
            }

            job.Status = IngestionStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Ingestion complete: {Documents} documents, {Chunks} chunks indexed",
                job.DocumentsProcessed, job.ChunksIndexed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion job {JobId} failed", job.Id);
            job.Status = IngestionStatus.Failed;
            job.Errors.Add(ex.Message);
            job.CompletedAt = DateTimeOffset.UtcNow;
        }

        return job;
    }

    public Task<IngestionJob> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(job);
        }

        throw new KeyNotFoundException($"Ingestion job '{jobId}' not found.");
    }
}
