using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  Agentic RAG — Index + Skillset + Indexer Setup
//  Run once after deploying Azure resources via Bicep
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  Agentic RAG — Pipeline Setup            ║");
Console.WriteLine("╚══════════════════════════════════════════╝\n");

// ── Load configuration from appsettings.json + environment variables ──
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

// ── STEP 1: Create Data Source (Blob) ──
Console.WriteLine("[1/5] Creating data source connection...");
var dataSource = new SearchIndexerDataSourceConnection(
    dataSourceName,
    SearchIndexerDataSourceType.AzureBlob,
    storageConnectionString,
    new SearchIndexerDataContainer(blobContainer));

await indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
Console.WriteLine("  ✓ Data source created.");

// ── STEP 2: Create Skillset (chunk + embed) ──
// NOTE: No DocumentExtractionSkill — the blob indexer natively extracts
// content into /document/content. Adding DocumentExtractionSkill causes
// a conflict (/document/file_data is unavailable with blob indexer).
// NOTE: Index projections are not exposed in Azure.Search.Documents SDK,
// so we use the REST API to create the skillset with projections.
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

// ── STEP 3: Create Index ──
Console.WriteLine("[3/5] Creating search index...");
var index = new SearchIndex(indexName)
{
    Fields = new List<SearchField>
    {
        // chunk_id uses keyword analyzer for exact-match lookups (required by index projections)
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

// ── STEP 4: Create Semantic Cache Index ──
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

// ── STEP 5: Create Indexer ──
// NOTE: No OutputFieldMappings — index projections in the skillset
// handle mapping chunks into the index. Having both causes conflicts.
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

// ── Reset & run the indexer to process existing documents ──
Console.WriteLine("Resetting and running indexer...");
await indexerClient.ResetIndexerAsync(indexerName);
await indexerClient.RunIndexerAsync(indexerName);
Console.WriteLine("  ✓ Indexer started. Documents will be processed shortly.\n");

Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("  All pipeline components created!");
Console.WriteLine("  Upload documents to the blob container");
Console.WriteLine("  and run the indexer again if needed.");
Console.WriteLine("═══════════════════════════════════════════");
