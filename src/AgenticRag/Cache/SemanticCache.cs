using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Embeddings;
using StackExchange.Redis;
using System.Numerics.Tensors;
using System.Text.Json;

namespace AgenticRag.Cache;

/// <summary>
/// Semantic cache using Redis Enterprise (RediSearch + HNSW vector index).
/// Caches Q&A pairs with embeddings. Cosine similarity >= threshold = cache hit.
/// </summary>
public sealed class SemanticCache
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly EmbeddingClient _embeddingClient;
    private readonly string _embeddingDeployment;
    private readonly int _dimensions;
    private readonly double _threshold;

    private const string IndexName = "semantic_cache_idx";
    private const string Prefix = "cache:";

    public SemanticCache(
        string redisConnection, string openAiEndpoint, string embeddingDeployment,
        int dimensions, double threshold, DefaultAzureCredential credential)
    {
        _redis = ConnectionMultiplexer.Connect(redisConnection);
        _db = _redis.GetDatabase();
        _embeddingDeployment = embeddingDeployment;
        _dimensions = dimensions;
        _threshold = threshold;

        var openAiClient = new AzureOpenAIClient(new Uri(openAiEndpoint), credential);
        _embeddingClient = openAiClient.GetEmbeddingClient(embeddingDeployment);

        EnsureIndex();
    }

    /// <summary>Look up a semantically similar question in cache.</summary>
    public async Task<string?> LookupAsync(string question)
    {
        var embedding = await EmbedAsync(question);
        var vecBytes = FloatsToBytes(embedding);

        var result = await _db.ExecuteAsync("FT.SEARCH", IndexName,
            $"(*)=>[KNN 1 @embedding $vec AS score]",
            "PARAMS", "2", "vec", vecBytes,
            "RETURN", "3", "question", "answer", "score",
            "SORTBY", "score",
            "DIALECT", "2");

        if (result is null) return null;

        var arr = (RedisResult[])result!;
        var total = (long)arr[0];
        if (total == 0) return null;

        // arr[1] = key, arr[2] = field array
        var fields = (RedisResult[])arr[2];
        string? answer = null;
        double score = 1.0;

        for (int i = 0; i < fields.Length; i += 2)
        {
            var name = (string)fields[i]!;
            var value = (string)fields[i + 1]!;
            if (name == "score") score = double.Parse(value);
            if (name == "answer") answer = value;
        }

        // COSINE distance → similarity
        var similarity = 1.0 - score;
        return similarity >= _threshold ? answer : null;
    }

    /// <summary>Store a question-answer pair in cache.</summary>
    public async Task StoreAsync(string question, string answer)
    {
        var embedding = await EmbedAsync(question);
        var key = $"{Prefix}{Guid.NewGuid():N}";

        var json = JsonSerializer.Serialize(new { question, answer, embedding });
        await _db.ExecuteAsync("JSON.SET", key, "$", json);
    }

    private async Task<float[]> EmbedAsync(string text)
    {
        var response = await _embeddingClient.GenerateEmbeddingAsync(text);
        return response.Value.ToFloats().ToArray();
    }

    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private void EnsureIndex()
    {
        try
        {
            _db.Execute("FT.INFO", IndexName);
        }
        catch
        {
            _db.Execute("FT.CREATE", IndexName,
                "ON", "JSON",
                "PREFIX", "1", Prefix,
                "SCHEMA",
                "$.question", "AS", "question", "TEXT",
                "$.answer", "AS", "answer", "TEXT",
                "$.embedding", "AS", "embedding", "VECTOR", "HNSW", "6",
                "TYPE", "FLOAT32", "DIM", _dimensions.ToString(), "DISTANCE_METRIC", "COSINE");
        }
    }
}
