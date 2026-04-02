// =====================================================================================
// AgenticRAG.Setup — ONE-TIME SETUP: Creates Azure AI Search index, skillset, and indexer
// =====================================================================================
//
// WHAT IS THIS?
// This console app creates ALL the infrastructure needed for document search:
//   Step 1: Data Source — connects Azure AI Search to Blob Storage (where PDFs live)
//   Step 2: Skillset — AI skills that chunk documents and generate embeddings
//   Step 3: Index — the search index with fields, vector config, and semantic config
//   Step 4: Cache Index — a separate index for semantic caching of QA pairs
//   Step 5: Indexer — the pipeline that pulls documents → runs skills → populates index
//
// WHEN TO RUN: Once after deploying Azure resources via Bicep. Re-run to recreate.
//
// KEY CONCEPTS FOR INTERVIEWS:
//   - Chunk + Embed: Documents are split into 2000-char chunks with 200-char overlap,
//     then each chunk is embedded into a vector using text-embedding-3-large
//   - Index Projections: Instead of storing whole-document records, the skillset
//     projects each CHUNK as a separate document in the index (one-to-many)
//   - No DocumentExtractionSkill: Blob indexer natively extracts content into
//     /document/content — adding DocumentExtractionSkill would cause a conflict
//
// INTERVIEW TIP: "The setup creates a chunking + embedding pipeline using Azure AI Search
// skillsets. Each PDF gets split into 2000-char chunks, embedded, and indexed for
// hybrid search (BM25 + vector + semantic reranking)."
// =====================================================================================
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  Agentic RAG — Pipeline Setup            ║");
Console.WriteLine("╚══════════════════════════════════════════╝\n");

// =====================================================================================
// CONFIGURATION — Read settings from appsettings.json for all Azure resource endpoints
// =====================================================================================
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var searchEndpoint = config["AzureAISearch:Endpoint"]
    ?? throw new InvalidOperationException("Missing AzureAISearch:Endpoint in appsettings.json");
var openAiEndpoint = config["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Missing AzureOpenAI:Endpoint in appsettings.json");
var openAiKey = config["AzureOpenAI:ApiKey"] ?? "";
var embeddingDeployment = config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-large";
var embeddingDimensions = int.Parse(config["AzureOpenAI:EmbeddingDimensions"] ?? "1536");
var storageConnectionString = config["BlobStorage:ConnectionString"]
    ?? throw new InvalidOperationException("Missing BlobStorage:ConnectionString in appsettings.json");
var blobContainer = config["BlobStorage:ContainerName"] ?? "documents";

var credential = new DefaultAzureCredential();
var indexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
var indexerClient = new SearchIndexerClient(new Uri(searchEndpoint), credential);

string indexName = config["AzureAISearch:IndexName"] ?? "agentic-rag-index";
string semanticConfig = config["AzureAISearch:SemanticConfig"] ?? "agentic-rag-semantic";
string skillsetName = "agentic-rag-skillset";
string indexerName = "agentic-rag-indexer";
string dataSourceName = "agentic-rag-datasource";

// =====================================================================================
// STEP 1: DATA SOURCE — Tell Azure AI Search where your documents are stored
// WHY: The indexer needs a data source connection to pull documents from Blob Storage
// HOW: Creates a connection pointing to the "documents" container in Azure Blob Storage
// =====================================================================================
Console.WriteLine("[1/5] Creating data source connection...");
var dataSource = new SearchIndexerDataSourceConnection(
    dataSourceName,
    SearchIndexerDataSourceType.AzureBlob,
    storageConnectionString,
    new SearchIndexerDataContainer(blobContainer));

await indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
Console.WriteLine("  ✓ Data source created.");

// =====================================================================================
// STEP 2: SKILLSET — AI pipeline that chunks documents and creates embeddings
// WHY: Raw PDFs can't be searched by meaning. We need to split them into chunks
//      and convert each chunk into a vector embedding for semantic search.
// HOW: Two skills in sequence:
//      1. SplitSkill: Splits /document/content into 2000-char pages with 200-char overlap
//      2. AzureOpenAIEmbeddingSkill: Converts each chunk into a vector using text-embedding-3-large
// IMPORTANT: No DocumentExtractionSkill — the blob indexer natively extracts content
//      into /document/content. Adding it would cause a "file_data unavailable" conflict.
// IMPORTANT: Index projections (at the bottom) make each chunk a separate search document
//      instead of storing one giant document. This is the one-to-many pattern.
// INTERVIEW TIP: "We use skillset index projections with skipIndexingParentDocuments
// to create one search document per chunk. This gives us fine-grained retrieval."
// =====================================================================================
Console.WriteLine("[2/5] Creating AI skillset via REST API...");

var skillsetJson = $$"""
{
  "name": "{{skillsetName}}",
  "description": "Agentic RAG skillset: chunk and embed",
  "skills": [
    {
      "@odata.type": "#Microsoft.Skills.Text.SplitSkill",
      "name": "text-splitter",
      "context": "/document",
      "textSplitMode": "pages",
      "maximumPageLength": 2000,
      "pageOverlapLength": 200,
      "defaultLanguageCode": "en",
      "inputs": [{ "name": "text", "source": "/document/content" }],
      "outputs": [{ "name": "textItems", "targetName": "chunks" }]
    },
    {
      "@odata.type": "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill",
      "name": "embedding-skill",
      "context": "/document/chunks/*",
      "resourceUri": "{{openAiEndpoint.TrimEnd('/')}}",
      "deploymentId": "{{embeddingDeployment}}",
      "modelName": "{{embeddingDeployment}}",
      "dimensions": {{embeddingDimensions}},
      {{(string.IsNullOrEmpty(openAiKey) ? "" : $"\"apiKey\": \"{openAiKey}\",")}}
      "inputs": [{ "name": "text", "source": "/document/chunks/*" }],
      "outputs": [{ "name": "embedding", "targetName": "content_vector" }]
    }
  ],
  "indexProjections": {
    "selectors": [
      {
        "targetIndexName": "{{indexName}}",
        "parentKeyFieldName": "document_id",
        "sourceContext": "/document/chunks/*",
        "mappings": [
          { "name": "content", "source": "/document/chunks/*" },
          { "name": "content_vector", "source": "/document/chunks/*/content_vector" },
          { "name": "title", "source": "/document/metadata_storage_name" }
        ]
      }
    ],
    "parameters": { "projectionMode": "skipIndexingParentDocuments" }
  }
}
""";

using var httpClient = new HttpClient();
var token = (await credential.GetTokenAsync(
    new Azure.Core.TokenRequestContext(new[] { "https://search.azure.com/.default" }))).Token;
httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

var skillsetUrl = $"{searchEndpoint.TrimEnd('/')}/skillsets/{skillsetName}?api-version=2024-07-01";
var skillsetRequest = new HttpRequestMessage(HttpMethod.Put, skillsetUrl)
{
    Content = new StringContent(skillsetJson, System.Text.Encoding.UTF8, "application/json")
};
skillsetRequest.Headers.Add("Prefer", "return=representation");

var skillsetResponse = await httpClient.SendAsync(skillsetRequest);
if (!skillsetResponse.IsSuccessStatusCode)
{
    var error = await skillsetResponse.Content.ReadAsStringAsync();
    throw new InvalidOperationException($"Failed to create skillset: {skillsetResponse.StatusCode} - {error}");
}
Console.WriteLine("  ✓ Skillset created.");

// =====================================================================================
// STEP 3: SEARCH INDEX — The main index where chunked documents are stored
// WHY: Azure AI Search needs an index schema defining fields, vector config, and semantic config
// HOW: Creates fields for chunk_id (key), content (searchable text), content_vector (embedding),
//      title, document_id, page_number, and metadata_storage_path
// VECTOR SEARCH: Uses HNSW (Hierarchical Navigable Small World) algorithm with cosine similarity
//   — This is the standard approximate nearest neighbor algorithm for vector search
// SEMANTIC SEARCH: Adds a semantic configuration that tells the reranker which fields
//   contain the main content and title — used during semantic reranking (L2 scoring)
// INTERVIEW TIP: "The index supports 3-layer hybrid search: BM25 keyword matching,
// vector similarity via HNSW, and semantic reranking via a cross-encoder model."
// =====================================================================================
Console.WriteLine("[3/5] Creating search index...");
var index = new SearchIndex(indexName)
{
    Fields = new List<SearchField>
    {
        // chunk_id is the primary key — uses "keyword" analyzer for exact-match lookups
        // (index projections generate chunk IDs like "docId-0", "docId-1", etc.)
        new SearchField("chunk_id", SearchFieldDataType.String)
        {
            IsKey = true,
            IsFilterable = true,
            IsSearchable = true,
            AnalyzerName = "keyword"
        },
        new SearchableField("content") { AnalyzerName = LexicalAnalyzerName.EnMicrosoft },
        new SearchableField("title") { IsFilterable = true, IsSortable = true },
        new SimpleField("document_id", SearchFieldDataType.String) { IsFilterable = true },
        new SimpleField("page_number", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
        new SearchField("content_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
        {
            IsSearchable = true,
            VectorSearchDimensions = embeddingDimensions,
            VectorSearchProfileName = "vector-profile"
        },
        new SimpleField("metadata_storage_path", SearchFieldDataType.String) { IsFilterable = true }
    },
    VectorSearch = new VectorSearch
    {
        Profiles = { new VectorSearchProfile("vector-profile", "hnsw-config") },
        Algorithms = { new HnswAlgorithmConfiguration("hnsw-config")
        {
            Parameters = new HnswParameters { Metric = VectorSearchAlgorithmMetric.Cosine }
        }}
    },
    SemanticSearch = new SemanticSearch
    {
        Configurations =
        {
            new SemanticConfiguration(semanticConfig, new SemanticPrioritizedFields
            {
                ContentFields = { new SemanticField("content") },
                TitleField = new SemanticField("title")
            })
        }
    }
};

await indexClient.CreateOrUpdateIndexAsync(index);
Console.WriteLine("  ✓ Index created.");

// =====================================================================================
// STEP 4: SEMANTIC CACHE INDEX — Stores previously answered Q&A pairs as vectors
// WHY: If a user asks "What is RAG?" and someone already asked that, we can return
//      the cached answer instantly instead of running the full AI pipeline again.
//      This saves cost (no GPT call) and reduces latency to ~50ms.
// HOW: Stores question text + question vector + answer JSON + TTL
//      At query time, SemanticCacheService embeds the new question and does a
//      vector similarity search against this index (cosine > 0.95 = cache hit)
// INTERVIEW TIP: "We use a separate cache index with vector similarity search
// to implement semantic caching — even paraphrased questions get cache hits."
// =====================================================================================
Console.WriteLine("[4/5] Creating semantic cache index...");
var cacheIndex = new SearchIndex("semantic-cache")
{
    Fields = new List<SearchField>
    {
        new SimpleField("cache_id", SearchFieldDataType.String) { IsKey = true },
        new SearchField("question_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
        {
            IsSearchable = true,
            VectorSearchDimensions = embeddingDimensions,
            VectorSearchProfileName = "cache-profile"
        },
        new SearchableField("question_text"),
        new SimpleField("answer_json", SearchFieldDataType.String),
        new SimpleField("created_at", SearchFieldDataType.DateTimeOffset) { IsFilterable = true },
        new SimpleField("ttl_minutes", SearchFieldDataType.Int32)
    },
    VectorSearch = new VectorSearch
    {
        Profiles = { new VectorSearchProfile("cache-profile", "cache-hnsw") },
        Algorithms = { new HnswAlgorithmConfiguration("cache-hnsw")
        {
            Parameters = new HnswParameters { Metric = VectorSearchAlgorithmMetric.Cosine }
        }}
    }
};

await indexClient.CreateOrUpdateIndexAsync(cacheIndex);
Console.WriteLine("  ✓ Cache index created.");

// =====================================================================================
// STEP 5: INDEXER — The pipeline runner that connects data source → skillset → index
// WHY: The indexer is the "glue" — it pulls documents from Blob Storage, runs them
//      through the skillset (chunk + embed), and pushes results into the search index.
// IMPORTANT: No OutputFieldMappings here — the skillset's index projections handle
//      mapping chunks into the index. Having both causes field mapping conflicts.
// HOW: Processes 10 documents per batch. Fails fast (MaxFailedItems = 0).
//      After creation, we reset and run it to process any existing documents.
// INTERVIEW TIP: "The indexer runs the full document pipeline: blob extraction →
// text splitting → embedding generation → index insertion, all managed by Azure."
// =====================================================================================
Console.WriteLine("[5/5] Creating indexer...");
var indexer = new SearchIndexer(indexerName, dataSourceName, indexName)
{
    SkillsetName = skillsetName,
    Parameters = new IndexingParameters
    {
        BatchSize = 10,
        MaxFailedItems = 0,
        MaxFailedItemsPerBatch = 0
    },
    FieldMappings =
    {
        new FieldMapping("metadata_storage_path") { TargetFieldName = "metadata_storage_path" }
    }
};

await indexerClient.CreateOrUpdateIndexerAsync(indexer);
Console.WriteLine("  ✓ Indexer created.\n");

// Reset the indexer state and trigger a fresh run to process existing blobs
Console.WriteLine("Resetting and running indexer...");
await indexerClient.ResetIndexerAsync(indexerName);
await indexerClient.RunIndexerAsync(indexerName);
Console.WriteLine("  ✓ Indexer started. Documents will be processed shortly.\n");

Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("  All pipeline components created!");
Console.WriteLine("  Upload documents to the blob container");
Console.WriteLine("  and run the indexer again if needed.");
Console.WriteLine("═══════════════════════════════════════════");
