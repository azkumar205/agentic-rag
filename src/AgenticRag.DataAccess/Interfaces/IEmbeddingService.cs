namespace AgenticRag.DataAccess.Interfaces;

/// <summary>
/// Interface for generating text embeddings via Azure OpenAI.
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IEnumerable<float[]>> GenerateBatchEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
