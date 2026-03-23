using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticRag.DataAccess.Services;

/// <summary>
/// Provides semantic and hybrid search over Azure AI Search.
/// </summary>
public class SearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly AzureSearchOptions _options;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IOptions<AzureSearchOptions> options,
        IEmbeddingService embeddingService,
        ILogger<SearchService> logger)
    {
        _options = options.Value;
        _embeddingService = embeddingService;
        _logger = logger;

        var credential = new AzureKeyCredential(_options.ApiKey);
        _indexClient = new SearchIndexClient(new Uri(_options.Endpoint), credential);
        _searchClient = new SearchClient(new Uri(_options.Endpoint), _options.IndexName, credential);
    }

    public async Task IndexDocumentChunksAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexExistsAsync(cancellationToken);

        var documents = chunks.Select(c => new SearchDocument
        {
            ["id"] = c.Id,
            ["documentId"] = c.DocumentId,
            ["fileName"] = c.FileName,
            ["content"] = c.Content,
            ["contentType"] = c.ContentType.ToString(),
            ["chunkIndex"] = c.ChunkIndex,
            ["pageNumber"] = c.PageNumber,
            ["section"] = c.Section,
            ["embedding"] = c.Embedding,
            ["ingestedAt"] = c.IngestedAt
        }).ToList();

        var batch = IndexDocumentsBatch.Upload(documents);
        var result = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
        _logger.LogInformation("Indexed {Count} document chunks", documents.Count);
    }

    public async Task<IEnumerable<DocumentChunk>> SemanticSearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        var searchOptions = new SearchOptions
        {
            Size = topK,
            Select = { "id", "documentId", "fileName", "content", "contentType", "chunkIndex", "pageNumber", "section", "ingestedAt" },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(embedding)
                    {
                        KNearestNeighborsCount = topK,
                        Fields = { "embedding" }
                    }
                }
            }
        };

        var results = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);
        return MapToDocumentChunks(results.Value);
    }

    public async Task<IEnumerable<DocumentChunk>> HybridSearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        var searchOptions = new SearchOptions
        {
            Size = topK,
            Select = { "id", "documentId", "fileName", "content", "contentType", "chunkIndex", "pageNumber", "section", "ingestedAt" },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(embedding)
                    {
                        KNearestNeighborsCount = topK,
                        Fields = { "embedding" }
                    }
                }
            }
        };

        var results = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);
        return MapToDocumentChunks(results.Value);
    }

    private static IEnumerable<DocumentChunk> MapToDocumentChunks(SearchResults<SearchDocument> results)
    {
        var chunks = new List<DocumentChunk>();
        foreach (var result in results.GetResults())
        {
            var doc = result.Document;
            chunks.Add(new DocumentChunk
            {
                Id = doc["id"]?.ToString() ?? string.Empty,
                DocumentId = doc["documentId"]?.ToString() ?? string.Empty,
                FileName = doc["fileName"]?.ToString() ?? string.Empty,
                Content = doc["content"]?.ToString() ?? string.Empty,
                ContentType = Enum.TryParse<ContentType>(doc["contentType"]?.ToString(), out var ct) ? ct : ContentType.Text,
                ChunkIndex = doc["chunkIndex"] is int ci ? ci : 0,
                PageNumber = doc["pageNumber"] is int pn ? pn : 0,
                Section = doc["section"]?.ToString() ?? string.Empty,
            });
        }
        return chunks;
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _indexClient.GetIndexAsync(_options.IndexName, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Creating search index '{IndexName}'", _options.IndexName);
            var index = new SearchIndex(_options.IndexName)
            {
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                    new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchableField("fileName") { IsFilterable = true },
                    new SearchableField("content"),
                    new SimpleField("contentType", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("chunkIndex", SearchFieldDataType.Int32),
                    new SimpleField("pageNumber", SearchFieldDataType.Int32),
                    new SearchableField("section"),
                    new SimpleField("ingestedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new VectorSearchField("embedding", _options.VectorDimensions, "hnsw-config")
                },
                VectorSearch = new VectorSearch
                {
                    Algorithms = { new HnswAlgorithmConfiguration("hnsw-config") },
                    Profiles = { new VectorSearchProfile("hnsw-profile", "hnsw-config") }
                }
            };
            await _indexClient.CreateIndexAsync(index, cancellationToken);
        }
    }
}
