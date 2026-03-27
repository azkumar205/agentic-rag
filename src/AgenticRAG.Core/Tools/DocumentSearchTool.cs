// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// DocumentSearchTool — AI Tool for searching company documents.
//
// Wraps Azure AI Search with hybrid retrieval (keyword BM25 + vector HNSW
// + semantic reranking). The agent calls this when it needs contract terms,
// policy clauses, or any unstructured document content.
//
// How it works:
//   1. Embeds the query client-side via text-embedding-3-large
//   2. Sends hybrid search to Azure AI Search (BM25 + vector + semantic rerank)
//   3. Formats top-K results as "[DocSource N]" strings for GPT-4o consumption
//
// The [Description] attributes are what GPT-4o sees when deciding which
// tool to call — they're critical for correct tool selection.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.ComponentModel;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AgenticRAG.Core.Configuration;
using OpenAI.Embeddings;

namespace AgenticRAG.Core.Tools;

public class DocumentSearchTool
{
    private readonly SearchClient _searchClient;       // Azure AI Search SDK client
    private readonly EmbeddingClient _embeddingClient;  // Generates 3072-dim vectors for hybrid search
    private readonly string _semanticConfig;
    private readonly int _embeddingDimensions;

    public DocumentSearchTool(SearchClient searchClient, EmbeddingClient embeddingClient,
        AzureAISearchSettings searchSettings, AzureOpenAISettings openAiSettings)
    {
        _searchClient = searchClient;
        _embeddingClient = embeddingClient;
        _semanticConfig = searchSettings.SemanticConfig;
        _embeddingDimensions = openAiSettings.EmbeddingDimensions;
    }

    [Description("Search company documents (contracts, policies, reports, procedures). " +
                 "Use this for questions about document content, clauses, terms, policies. " +
                 "Returns relevant text passages with source document names.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query — be specific about what you're looking for")] string query,
        [Description("Number of results to return (default 5, max 10)")] int topK = 5)
    {
        Console.WriteLine($"[DocumentSearchTool] SearchDocumentsAsync called with query='{query}'");
        try
        {
        topK = Math.Clamp(topK, 1, 10);

        // Generate embedding client-side
        var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = _embeddingDimensions };
        var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(query, embeddingOptions);
        ReadOnlyMemory<float> queryVector = embeddingResult.Value.ToFloats();

        var options = new SearchOptions
        {
            Size = topK,
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _semanticConfig,
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive)
            },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = topK * 2,
                        Fields = { "content_vector" }
                    }
                }
            },
            Select = { "chunk_id", "content", "title" }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(query, options);
        var results = new List<string>();
        int index = 1;

        await foreach (var result in response.Value.GetResultsAsync())
        {
            var title = result.Document.GetString("title");
            var content = result.Document.GetString("content");
            var score = result.SemanticSearch?.RerankerScore ?? result.Score ?? 0;

            results.Add($"[DocSource {index}] (Title: {title}, Score: {score:F2})\n{content}");
            index++;
        }

        var output = results.Count > 0
            ? string.Join("\n\n---\n\n", results)
            : "No relevant documents found for this query.";
        Console.WriteLine($"[DocumentSearchTool] Returning {results.Count} results");
        return output;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DocumentSearchTool] ERROR: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
