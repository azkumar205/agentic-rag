// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Configuration — Strongly-typed settings bound from appsettings.json.
// Each class maps to a config section (e.g., "AzureAISearch", "Agent").
// See appsettings.json for the actual values and descriptions.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
namespace AgenticRAG.Core.Configuration;

public class AzureAISearchSettings
{
    public string Endpoint { get; set; } = "";
    public string IndexName { get; set; } = "agentic-rag-index";
    public string SemanticConfig { get; set; } = "agentic-rag-semantic";
}

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = "";
    public string ChatDeployment { get; set; } = "gpt-4o";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-large";
    public int EmbeddingDimensions { get; set; } = 1536;
}

public class SqlServerSettings
{
    public string ConnectionString { get; set; } = "";
    public List<string> AllowedViews { get; set; } = new();
}

public class BlobStorageSettings
{
    public string AccountName { get; set; } = "";
    public string ContainerName { get; set; } = "documents";
    public string ImageContainerName { get; set; } = "images";
}

public class RedisSettings
{
    public string ConnectionString { get; set; } = "";
}

public class AgentSettings
{
    public int MaxHistoryTurns { get; set; } = 6;
    public int SummarizeAfterTurns { get; set; } = 10;
    public int ReflectionThreshold { get; set; } = 6;
    public int MaxReflectionRetries { get; set; } = 2;
    public double SemanticCacheThreshold { get; set; } = 0.92;
    public int CacheTtlMinutes { get; set; } = 60;
}

public class GoogleWebSearchSettings
{
    public string ApiKey { get; set; } = "";
    public string SearchEngineId { get; set; } = "";
    public string Endpoint { get; set; } = "https://www.googleapis.com/customsearch/v1";
}

public class McpProxySettings
{
    public string Endpoint { get; set; } = "http://localhost:5000/mcp";
}
