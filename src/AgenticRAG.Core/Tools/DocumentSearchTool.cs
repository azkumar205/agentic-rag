// =====================================================================================
// DocumentSearchTool — AI TOOL: Searches company documents using Azure AI Search
// =====================================================================================
//
// WHAT IS THIS?
// This is one of the agent's TOOLS — when GPT-4o decides it needs document content
// (contracts, policies, reports), it calls this tool. The tool searches Azure AI Search
// using a 3-layer hybrid retrieval strategy:
//
// HOW DOES HYBRID SEARCH WORK? (3 layers, each catches what others miss)
//   Layer 1: BM25 (keyword) — traditional text matching ("invoice number 12345")
//   Layer 2: Vector (HNSW)  — semantic meaning matching ("payment obligations" ≈ "amounts due")
//   Layer 3: Semantic Rerank — Microsoft's cross-encoder reranks the combined results
//   Result: Best of both worlds — exact keywords AND semantic understanding
//
// THE FLOW:
//   1. User asks: "What are the payment terms in Contoso's contract?"
//   2. GPT-4o decides to call SearchDocumentsAsync (via MCP proxy)
//   3. This tool embeds the query into a 3072-dim vector using text-embedding-3-large
//   4. Sends hybrid search to Azure AI Search (BM25 + vector + semantic rerank)
//   5. Returns top-K results formatted as "[DocSource N]" for GPT-4o to cite
//
// WHY [Description] ATTRIBUTES MATTER:
// GPT-4o reads these descriptions to decide WHICH tool to call. If the description
// says "contracts, policies, reports" and the user asks about a policy, GPT-4o
// will select this tool. Bad descriptions = wrong tool = wrong answer.
//
// INTERVIEW TIP: "We use hybrid search — BM25 for exact keywords plus vector search
// for semantic meaning, with semantic reranking on top. This gives the best retrieval."
// =====================================================================================
using System.ComponentModel;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AgenticRAG.Core.Configuration;
using OpenAI.Embeddings;

namespace AgenticRAG.Core.Tools;

public class DocumentSearchTool
{
    private readonly SearchClient _searchClient;        // Azure AI Search SDK client (points to main index)
    private readonly EmbeddingClient _embeddingClient;   // text-embedding-3-large for 3072-dim vectors
    private readonly string _semanticConfig;             // Name of the semantic configuration in the index
    private readonly int _embeddingDimensions;           // 3072 for document search (large model)

    public DocumentSearchTool(SearchClient searchClient, EmbeddingClient embeddingClient,
        AzureAISearchSettings searchSettings, AzureOpenAISettings openAiSettings)
    {
        _searchClient = searchClient;
        _embeddingClient = embeddingClient;
        _semanticConfig = searchSettings.SemanticConfig;
        _embeddingDimensions = openAiSettings.EmbeddingDimensions;
    }

    // This [Description] is what GPT-4o reads when deciding which tool to call
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

        // Step 1: Generate a 3072-dimensional embedding of the search query
        var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = _embeddingDimensions };
        var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(query, embeddingOptions);
        ReadOnlyMemory<float> queryVector = embeddingResult.Value.ToFloats();

        // Step 2: Build hybrid search options (keyword + vector + semantic rerank)
        var options = new SearchOptions
        {
            Size = topK,
            QueryType = SearchQueryType.Semantic,  // Enables semantic reranking (Layer 3)
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _semanticConfig,
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive)  // Also extract highlights
            },
            VectorSearch = new VectorSearchOptions  // Vector search (Layer 2)
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = topK * 2,  // Search wider, then rerank narrows down
                        Fields = { "content_vector" }
                    }
                }
            },
            Select = { "chunk_id", "content", "title" }  // Only return the fields we need
        };

        // Step 3: Execute hybrid search — Azure AI Search combines BM25 + vector results
        var response = await _searchClient.SearchAsync<SearchDocument>(query, options);
        var results = new List<string>();
        int index = 1;

        // Step 4: Format results as "[DocSource N]" — GPT-4o will cite these in its answer
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
            // Return error as text — GPT-4o can read this and try a different approach
            Console.WriteLine($"[DocumentSearchTool] ERROR: {ex.GetType().Name}: {ex.Message}");
            return $"[DocSearch Error] Document search failed: {ex.Message}";
        }
    }
}
