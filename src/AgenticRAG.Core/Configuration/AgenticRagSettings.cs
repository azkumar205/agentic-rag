// =====================================================================================
// AgenticRagSettings.cs — ALL CONFIGURATION CLASSES in one place
// =====================================================================================
//
// WHAT IS THIS?
// Every setting from appsettings.json gets mapped to a C# class here.
// Example: "AzureAISearch": { "Endpoint": "..." } → AzureAISearchSettings.Endpoint
// This gives us type safety, IntelliSense, and no magic strings throughout the codebase.
//
// HOW IT CONNECTS:
// Program.cs reads JSON → creates these objects → registers as Singletons in DI →
// any service can receive the settings it needs via constructor injection.
//
// INTERVIEW TIP: "We use Options pattern with strongly-typed settings. Each Azure service
// has its own config class. Zero magic strings, full IntelliSense, easy to swap environments."
// =====================================================================================
namespace AgenticRAG.Core.Configuration;

// Azure AI Search — where company documents (contracts, policies) are indexed and searched
public class AzureAISearchSettings
{
    public string Endpoint { get; set; } = "";
    public string IndexName { get; set; } = "agentic-rag-index";
    public string SemanticConfig { get; set; } = "agentic-rag-semantic";
}

// Azure OpenAI — hosts our GPT models and embedding models
// TWO chat models: GPT-4o (expensive, for complex answers) and GPT-4o-mini (cheap, for planning)
// TWO embedding models: large (for docs) and small (for cache) — see Program.cs for why
public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = "";
    public string ChatDeployment { get; set; } = "gpt-4o";              // For complex generation
    public string PlanningDeployment { get; set; } = "gpt-4o-mini";     // For tool selection, reflection
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-large";  // For document search
    public int EmbeddingDimensions { get; set; } = 1536;                // Dimensions for doc embeddings
    public string CacheEmbeddingDeployment { get; set; } = "text-embedding-3-small"; // For cache (cheaper)
    public int CacheEmbeddingDimensions { get; set; } = 512;           // Smaller = faster + cheaper
}

// SQL Server — read-only views for billing, invoices, contracts, vendor data
public class SqlServerSettings
{
    public string ConnectionString { get; set; } = "";
    public List<string> AllowedViews { get; set; } = new(); // Whitelist: only these views can be queried
}

// Azure Blob Storage — stores the uploaded PDF documents and extracted images
public class BlobStorageSettings
{
    public string AccountName { get; set; } = "";
    public string ContainerName { get; set; } = "documents";    // Raw PDFs
    public string ImageContainerName { get; set; } = "images";  // Extracted charts/diagrams
}

// Redis — stores conversation history per session (multi-turn memory)
public class RedisSettings
{
    public string ConnectionString { get; set; } = "";
}

// Agent behavior settings — tunable knobs for the orchestrator pipeline
public class AgentSettings
{
    public int MaxHistoryTurns { get; set; } = 6;              // Max conversation turns to send to LLM
    public int SummarizeAfterTurns { get; set; } = 10;         // After this many turns, summarize older ones
    public int ReflectionThreshold { get; set; } = 6;          // Min quality score (1-10) to accept answer
    public int MaxReflectionRetries { get; set; } = 2;         // How many times to retry if score is low
    public double SemanticCacheThreshold { get; set; } = 0.92; // Cosine similarity to consider cache hit
    public int CacheTtlMinutes { get; set; } = 60;             // How long cached answers live
}

// =====================================================================================
// QueryRewriteSettings — Controls the pre-retrieval query rewriting step
// =====================================================================================
// WHY THIS EXISTS:
// Users often ask short or vague questions. Rewriting makes the query clearer while
// preserving original intent, improving retrieval quality with minimal extra cost.
//
// SAFETY:
// - Feature-flagged (off by default)
// - If rewriting fails/timeouts, pipeline uses original question (no hard failure)
// =====================================================================================
public class QueryRewriteSettings
{
    public bool Enabled { get; set; } = false;
    public int MaxRewriteChars { get; set; } = 512;
    public double MinRewriteConfidence { get; set; } = 0.6;
}

// =====================================================================================
// AmbiguitySettings — Controls clarify-first behavior for vague user questions
// =====================================================================================
// WHAT IT DOES:
// If a question is too ambiguous (missing entity, timeframe, or scope), the orchestrator
// returns a clarification payload instead of running expensive retrieval/generation blindly.
// =====================================================================================
public class AmbiguitySettings
{
    public bool Enabled { get; set; } = false;
    public double ClarificationThreshold { get; set; } = 0.75;
    public int MaxClarificationQuestions { get; set; } = 3;
    public bool AllowFreeText { get; set; } = true;
}

// Google Web Search — for questions that need public internet data
public class GoogleWebSearchSettings
{
    public string ApiKey { get; set; } = "";
    public string SearchEngineId { get; set; } = "";
    public string Endpoint { get; set; } = "https://www.googleapis.com/customsearch/v1";
}

// MCP Proxy — the URL where our MCP server endpoint lives (usually same host)
public class McpProxySettings
{
    public string Endpoint { get; set; } = "http://localhost:5000/mcp";
}

// =====================================================================================
// PiiSettings — Controls PII (Personally Identifiable Information) detection & redaction
// =====================================================================================
//
// WHAT IS PII REDACTION?
// Users might accidentally include sensitive data: "My SSN is 123-45-6789, find my contract"
// Documents and SQL results may contain emails, phone numbers, addresses.
// This service detects and replaces PII before it reaches the LLM, cache, or logs.
//
// 5 DEFENSE LAYERS (each can be toggled on/off independently):
// Layer 1: RedactUserInput     — Clean the user's question BEFORE the LLM sees it
// Layer 2: RedactToolResults   — Clean document/SQL/web results BEFORE generation
// Layer 3: RedactFinalAnswer   — Clean the answer BEFORE returning to the client
// Layer 4: RedactBeforeCaching — Clean BEFORE writing to cache (shared across users!)
// Layer 5: RedactBeforeMemory  — Clean BEFORE writing to Redis session history
//
// 3 REDACTION MODES:
// Mask:    "john@acme.com" → "[EMAIL_REDACTED]"          (safest, default)
// Partial: "john@acme.com" → "j***@***.com"              (some UX value)
// Hash:    "john@acme.com" → "[EMAIL:a1b2c3d4]"          (allows correlation)
//
// INTERVIEW TIP: "We have defense-in-depth for PII — 5 layers independently toggled.
// Cache is the most critical because cached answers can be returned to ANY user."
// =====================================================================================
public class PiiSettings
{
    public bool Enabled { get; set; } = true;
    public string RedactionMode { get; set; } = "Mask";
    public List<string> EnabledEntities { get; set; } = new()
    {
        "SSN", "CreditCard", "Email", "Phone", "IpAddress",
        "DateOfBirth", "PassportNumber", "BankAccount", "Address"
    };

    public bool RedactUserInput { get; set; } = true;      // Layer 1: before LLM sees user question
    public bool RedactToolResults { get; set; } = true;     // Layer 2: before LLM sees tool results
    public bool RedactFinalAnswer { get; set; } = true;     // Layer 3: before response goes to client
    public bool RedactBeforeCaching { get; set; } = true;   // Layer 4: before shared cache write
    public bool RedactBeforeMemory { get; set; } = true;    // Layer 5: before Redis session write

    // Converts string entity names ("SSN", "Email") to the PiiEntityType enum.
    // Invalid names are silently skipped — prevents crash on typos in config.
    public List<Privacy.PiiEntityType> GetParsedEntities()
    {
        var parsed = new List<Privacy.PiiEntityType>();
        foreach (var name in EnabledEntities)
        {
            if (Enum.TryParse<Privacy.PiiEntityType>(name, ignoreCase: true, out var entity))
                parsed.Add(entity);
        }
        return parsed;
    }
}
