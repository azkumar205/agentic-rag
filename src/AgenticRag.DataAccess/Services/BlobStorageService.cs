using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticRag.DataAccess.Services;

/// <summary>
/// Manages document retrieval and upload from Azure Blob Storage.
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IOptions<AzureStorageOptions> options, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = new BlobServiceClient(options.Value.ConnectionString);
        _logger = logger;
    }

    public async Task<IEnumerable<string>> ListNewDocumentsAsync(
        DateTimeOffset? since,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobNames = new List<string>();

        await foreach (BlobItem blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (since == null || blob.Properties.LastModified > since)
            {
                blobNames.Add(blob.Name);
            }
        }

        _logger.LogInformation("Found {Count} new/updated documents in container '{Container}' since {Since}",
            blobNames.Count, containerName, since);

        return blobNames;
    }

    public async Task<Stream> DownloadDocumentAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return download.Value.Content;
    }

    public async Task UploadDocumentAsync(
        string containerName,
        string blobName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
        _logger.LogInformation("Uploaded document '{BlobName}' to container '{Container}'", blobName, containerName);
    }
}
