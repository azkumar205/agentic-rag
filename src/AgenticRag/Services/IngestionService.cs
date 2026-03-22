using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;
using AgenticRag.Tools;

namespace AgenticRag.Services;

/// <summary>
/// Ingestion Service — Implements the full Classic RAG ingestion pipeline:
/// Extract text (Document Intelligence) → Chunk → Embed (text-embedding-3-large)
/// → Upload to Azure AI Search index.
///
/// This is the foundation of the RAG system. Run once (or whenever new documents
/// are added) to populate the Azure AI Search index that all query-time tools use.
/// </summary>
public sealed class IngestionService
{
    private readonly DocumentIntelligenceTool _docTool;
    private readonly EmbeddingClient _embeddingClient;
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;
    private readonly int _embeddingDimensions;

    public IngestionService(
        string searchEndpoint,
        string indexName,
        string openAiEndpoint,
        string embeddingDeployment,
        int embeddingDimensions,
        DefaultAzureCredential credential,
        DocumentIntelligenceTool docTool)
    {
        _docTool = docTool;
        _indexName = indexName;
        _embeddingDimensions = embeddingDimensions;

        var oaiClient = new AzureOpenAIClient(new Uri(openAiEndpoint), credential);
        _embeddingClient = oaiClient.GetEmbeddingClient(embeddingDeployment);
        _searchClient = new SearchClient(new Uri(searchEndpoint), indexName, credential);
        _indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
    }

    /// <summary>
    /// Ensures the Azure AI Search index exists with the correct schema.
    /// Creates or updates the index with:
    ///   - id (key), content (searchable), source (filterable), title (searchable)
    ///   - content_vector (3072-dim HNSW vector profile for semantic search)
    ///   - Semantic configuration named "default" for hybrid + semantic ranking
    /// Safe to call on every startup — CreateOrUpdateIndex is idempotent.
    /// </summary>
    public async Task EnsureIndexExistsAsync()
    {
        var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String)
            {
                IsKey = true,
                IsFilterable = true
            },
            new SearchableField("content")
            {
                IsFilterable = false
            },
            new SimpleField("source", SearchFieldDataType.String)
            {
                IsFilterable = true,
                IsFacetable = false
            },
            new SearchableField("title")
            {
                IsFilterable = false
            },
            new VectorSearchField("content_vector", _embeddingDimensions, "hnsw-config")
        };

        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("hnsw-algo"));
        vectorSearch.Profiles.Add(new VectorSearchProfile("hnsw-config", "hnsw-algo"));

        var semanticConfig = new SemanticConfiguration(
            "default",
            new SemanticPrioritizedFields
            {
                ContentFields = { new SemanticField("content") },
                TitleField = new SemanticField("title")
            });

        var semanticSearch = new SemanticSearch();
        semanticSearch.Configurations.Add(semanticConfig);

        var index = new SearchIndex(_indexName)
        {
            Fields = fields,
            VectorSearch = vectorSearch,
            SemanticSearch = semanticSearch
        };

        await _indexClient.CreateOrUpdateIndexAsync(index);
    }

    /// <summary>
    /// Full ingestion pipeline for a single file:
    ///   Step 1 — Extract text using Document Intelligence (supports PDF, DOCX, images)
    ///   Step 2 — Chunk text into overlapping windows
    ///   Step 3 — Embed each chunk with text-embedding-3-large (3072-dim vectors)
    ///   Step 4 — Upload chunks + vectors to Azure AI Search in batches of 100
    /// </summary>
    /// <param name="filePath">Local path to the file to ingest.</param>
    /// <param name="chunkSize">Maximum characters per chunk (default 1000).</param>
    /// <param name="overlap">Overlap between consecutive chunks in characters (default 200).</param>
    /// <returns>Summary with chunks created, characters processed, and file name.</returns>
    public async Task<IngestionResult> IngestFileAsync(
        string filePath, int chunkSize = 1000, int overlap = 200)
    {
        // Step 1: Extract full text from document using Document Intelligence
        var text = await _docTool.ExtractAsync(filePath);

        // Step 2: Chunk the extracted text with overlap
        var chunks = ChunkText(text, chunkSize, overlap).ToList();

        // Step 3: Embed each chunk and build Azure AI Search documents
        var fileName = Path.GetFileName(filePath);
        var docs = new List<SearchDocument>(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var embedding = await _embeddingClient.GenerateEmbeddingAsync(chunks[i]);
            docs.Add(new SearchDocument
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["content"] = chunks[i],
                ["source"] = filePath,
                ["title"] = $"{fileName} - chunk {i + 1}",
                ["content_vector"] = embedding.Value.ToFloats().ToArray()
            });
        }

        // Step 4: Upload to Azure AI Search in batches of 100
        for (int i = 0; i < docs.Count; i += 100)
        {
            var batch = docs.GetRange(i, Math.Min(100, docs.Count - i));
            await _searchClient.UploadDocumentsAsync(batch);
        }

        return new IngestionResult(
            FileName: fileName,
            ChunksCreated: docs.Count,
            CharactersProcessed: text.Length
        );
    }

    /// <summary>
    /// Splits text into overlapping chunks.
    /// Overlap preserves context at chunk boundaries so sentences aren't cut off.
    /// Stops when the remaining text after the current start is smaller than the overlap,
    /// which would produce a chunk entirely contained within the previous one.
    /// </summary>
    private static IEnumerable<string> ChunkText(string text, int size, int overlap)
    {
        for (int start = 0; start < text.Length; start += size - overlap)
        {
            // Stop if the remaining text is fully covered by the previous chunk's overlap
            if (start > 0 && start + overlap >= text.Length)
                yield break;
            yield return text[start..Math.Min(start + size, text.Length)];
        }
    }
}

/// <summary>Result of a document ingestion operation.</summary>
public record IngestionResult(
    string FileName,
    int ChunksCreated,
    int CharactersProcessed);
