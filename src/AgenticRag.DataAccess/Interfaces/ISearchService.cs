using AgenticRag.Shared.Models;

namespace AgenticRag.DataAccess.Interfaces;

/// <summary>
/// Interface for Azure AI Search operations.
/// </summary>
public interface ISearchService
{
    Task IndexDocumentChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentChunk>> SemanticSearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentChunk>> HybridSearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}
