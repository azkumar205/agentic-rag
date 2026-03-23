using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ClientModel;

namespace AgenticRag.DataAccess.Services;

/// <summary>
/// Generates text embeddings using Azure OpenAI's embedding models.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IOptions<AzureOpenAiOptions> options, ILogger<EmbeddingService> logger)
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(options.Value.Endpoint),
            new ApiKeyCredential(options.Value.ApiKey));
        _embeddingClient = azureClient.GetEmbeddingClient(options.Value.EmbeddingDeployment);
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<IEnumerable<float[]>> GenerateBatchEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        _logger.LogInformation("Generating embeddings for {Count} texts", textList.Count);

        var response = await _embeddingClient.GenerateEmbeddingsAsync(textList, cancellationToken: cancellationToken);
        return response.Value.Select(e => e.ToFloats().ToArray());
    }
}
