using AgenticRag.Shared.Models;

namespace AgenticRag.DataAccess.Interfaces;

/// <summary>
/// Interface for multimodal document extraction (text, images, tables, OCR).
/// </summary>
public interface IDocumentExtractionService
{
    Task<IEnumerable<DocumentChunk>> ExtractChunksAsync(Stream documentStream, string fileName, CancellationToken cancellationToken = default);
}
