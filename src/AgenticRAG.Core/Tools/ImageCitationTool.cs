// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// ImageCitationTool — AI Tool for retrieving document images.
//
// Enables multimodal responses: when the agent needs visual content
// (charts, diagrams, scanned tables), it lists matching blobs in the
// "images" container and generates time-limited SAS download URLs.
//
// Images are pre-extracted from PDFs via Azure Document Intelligence
// and stored as: {docName}/page{N}-figure{id}.png in Blob Storage.
// SAS URLs are read-only, valid for 1 hour, no auth needed by client.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using AgenticRAG.Core.Configuration;

namespace AgenticRAG.Core.Tools;

public class ImageCitationTool
{
    private readonly BlobServiceClient _blobClient;    // Azure Blob Storage SDK client
    private readonly string _imagesContainer;           // Container name (default: "images")

    public ImageCitationTool(BlobServiceClient blobClient, BlobStorageSettings settings)
    {
        _blobClient = blobClient;
        _imagesContainer = settings.ImageContainerName;
    }

    [Description("Get images (charts, diagrams, tables as images, scanned pages) from a document. " +
                 "Returns downloadable URLs. Use when the user asks about visual content, " +
                 "charts, diagrams, or when a document image would support the answer.")]
    public async Task<string> GetDocumentImagesAsync(
        [Description("Document filename (e.g., 'acme-contract.pdf')")] string documentName,
        [Description("Optional: specific page number to get images from")] int? pageNumber = null)
    {
        var container = _blobClient.GetBlobContainerClient(_imagesContainer);

        var prefix = Path.GetFileNameWithoutExtension(documentName);
        var images = new List<string>();
        int index = 1;

        await foreach (var blob in container.GetBlobsAsync(prefix: prefix))
        {
            if (pageNumber.HasValue && !blob.Name.Contains($"page{pageNumber}"))
                continue;

            var blobClient = container.GetBlobClient(blob.Name);

            var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddHours(1));

            images.Add($"[Image {index}] {blob.Name}\n  Download: {sasUri}\n  Size: {blob.Properties.ContentLength} bytes");
            index++;

            if (index > 10) break;
        }

        return images.Count > 0
            ? $"Found {images.Count} image(s) from '{documentName}':\n\n" + string.Join("\n\n", images)
            : $"No images found for document '{documentName}'.";
    }
}
