namespace AgenticRag.DataAccess.Interfaces;

/// <summary>
/// Interface for Azure Blob Storage operations.
/// </summary>
public interface IBlobStorageService
{
    Task<IEnumerable<string>> ListNewDocumentsAsync(DateTimeOffset? since, string containerName, CancellationToken cancellationToken = default);
    Task<Stream> DownloadDocumentAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
    Task UploadDocumentAsync(string containerName, string blobName, Stream content, CancellationToken cancellationToken = default);
}
