// =====================================================================================
// ImageCitationTool — AI TOOL: Retrieves document images (charts, diagrams, tables)
// =====================================================================================
//
// WHAT IS THIS?
// Enables MULTIMODAL responses: when the agent needs visual content (charts, diagrams,
// scanned tables), it lists matching blobs in Azure Blob Storage and generates
// time-limited SAS download URLs that the frontend can display directly.
//
// HOW ARE IMAGES STORED?
// PDFs are pre-processed by Azure Document Intelligence (separate pipeline).
// Extracted images are stored as: {docName}/page{N}-figure{id}.png in Blob Storage.
//
// SECURITY: SAS URLs are read-only, valid for 1 hour, no auth needed by the client.
// After 1 hour they expire — no permanent public URLs are ever created.
//
// MAX 10 images returned per request to prevent huge response payloads.
//
// INTERVIEW TIP: "Our RAG supports multimodal output — the agent can include charts
// and diagrams from documents using SAS-protected download URLs."
// =====================================================================================
using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using AgenticRAG.Core.Configuration;

namespace AgenticRAG.Core.Tools;

public class ImageCitationTool
{
    private readonly BlobServiceClient _blobClient;     // Azure Blob Storage SDK client
    private readonly string _imagesContainer;            // Container name (default: "images")

    public ImageCitationTool(BlobServiceClient blobClient, BlobStorageSettings settings)
    {
        _blobClient = blobClient;
        _imagesContainer = settings.ImageContainerName;
    }

    // GPT-4o calls this when the user asks about visual content or when an image would help the answer
    [Description("Get images (charts, diagrams, tables as images, scanned pages) from a document. " +
                 "Returns downloadable URLs. Use when the user asks about visual content, " +
                 "charts, diagrams, or when a document image would support the answer.")]
    public async Task<string> GetDocumentImagesAsync(
        [Description("Document filename (e.g., 'acme-contract.pdf')")] string documentName,
        [Description("Optional: specific page number to get images from")] int? pageNumber = null)
    {
        var container = _blobClient.GetBlobContainerClient(_imagesContainer);

        // Images are stored with the document name as the blob prefix
        var prefix = Path.GetFileNameWithoutExtension(documentName);
        var images = new List<string>();
        int index = 1;

        // List all blobs matching this document's prefix
        await foreach (var blob in container.GetBlobsAsync(prefix: prefix))
        {
            // If a specific page was requested, filter by page number
            if (pageNumber.HasValue && !blob.Name.Contains($"page{pageNumber}"))
                continue;

            var blobClient = container.GetBlobClient(blob.Name);

            // Generate a read-only SAS URL valid for 1 hour — no auth needed by client
            var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddHours(1));

            images.Add($"[Image {index}] {blob.Name}\n  Download: {sasUri}\n  Size: {blob.Properties.ContentLength} bytes");
            index++;

            // Cap at 10 images to prevent huge response payloads
            if (index > 10) break;
        }

        return images.Count > 0
            ? $"Found {images.Count} image(s) from '{documentName}':\n\n" + string.Join("\n\n", images)
            : $"No images found for document '{documentName}'.";
    }
}
