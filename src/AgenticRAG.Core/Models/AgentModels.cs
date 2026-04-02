// =====================================================================================
// AgentModels.cs — THE DATA CONTRACTS between frontend and backend
// =====================================================================================
//
// WHAT IS THIS?
// These are the C# classes that define WHAT the API receives and WHAT it returns.
// Think of them as the "shape" of the JSON that flows between client and server.
//
// TWO MAIN CLASSES:
// AgentRequest  = What the frontend SENDS   → { question, sessionId, topK }
// AgentResponse = What the API RETURNS      → { answer, citations, toolsUsed, piiSummary, ... }
//
// THE RESPONSE IS RICH — it tells the frontend everything:
// - The answer text with inline citations like [DocSource 1], [SQLSource]
// - Which tools were called (SearchDocuments, QuerySql, SearchWeb)
// - The reasoning steps the agent took (like a "show your work" trail)
// - Any errors that occurred and whether fallbacks recovered from them
// - PII redaction stats (how many items were redacted, by type and layer)
// - Which model was used (gpt-4o-mini or gpt-4o) and why (cost optimization)
// - Whether the answer came from cache (instant) or was freshly generated
//
// INTERVIEW TIP: "Our API response includes full observability — tool usage, reasoning
// steps, error recovery status, PII stats, and model routing info. The frontend can
// display: 'Answered from 2 documents + 1 SQL query | 3 PII items redacted | via GPT-4o'."
// =====================================================================================
namespace AgenticRAG.Core.Models;

// What the frontend sends to POST /api/agent/ask
public class AgentRequest
{
    public string Question { get; set; } = "";       // The user's question (required)
    public string? SessionId { get; set; }            // For multi-turn conversations (optional)
    public string? Category { get; set; }             // Future: filter by document category
    public int TopK { get; set; } = 5;                // How many search results to retrieve
}

// What the API returns — a complete package of answer + metadata
public class AgentResponse
{
    public string Answer { get; set; } = "";                          // The generated answer text
    public List<TextCitation> TextCitations { get; set; } = new();    // Document/SQL/Web sources used
    public List<ImageCitation> ImageCitations { get; set; } = new();  // Charts/diagrams from documents
    public List<string> ToolsUsed { get; set; } = new();              // Which MCP tools were called
    public List<string> ReasoningSteps { get; set; } = new();         // Step-by-step agent decisions
    public List<AgentError> Errors { get; set; } = new();             // Any tool failures + recovery
    public PiiSummary PiiRedaction { get; set; } = new();             // PII stats (counts only, no PII)
    public int ReflectionScore { get; set; }                          // Quality score 1-10 from self-eval
    public bool FromCache { get; set; }                               // True = instant cache hit, no LLM used
    public string ModelUsed { get; set; } = "";                       // "gpt-4o-mini" or "gpt-4o"
    public TokenUsageInfo TokenUsage { get; set; } = new();           // Token count + cost estimate
    public string SessionId { get; set; } = "";                       // Session ID for follow-up questions
    public bool AwaitingClarification { get; set; }                    // True = ask follow-up before retrieval
    public string? ClarificationId { get; set; }                       // Correlates clarification turn
    public ClarificationRequest? ClarificationRequest { get; set; }    // Structured clarification payload
    public QueryRewriteInfo? QueryRewrite { get; set; }                // Observability for rewrite step
    // Similar past questions shown to user when their query is unclear or could be improved.
    // Populated by cache-based suggestion search — zero extra LLM cost.
    public List<string> SuggestedQuestions { get; set; } = new();
}

// A reference to a document chunk, SQL result, or web snippet used in the answer.
// The frontend can show: "Source: acme-contract.pdf (relevance: 0.95)"
public class TextCitation
{
    public int Index { get; set; }                    // Matches [DocSource N] in the answer text
    public string SourceDocument { get; set; } = "";  // Document filename
    public string Content { get; set; } = "";         // The actual text chunk used
    public double RelevanceScore { get; set; }        // How relevant this chunk was (0-1)
    public string SourceType { get; set; } = "document"; // "document", "sql", or "web"
}

// A downloadable image (chart, diagram, scanned table) from a document.
// Frontend can render these inline with the answer.
public class ImageCitation
{
    public int Index { get; set; }
    public string FileName { get; set; } = "";        // e.g., "acme-contract/page3-figure1.png"
    public string DownloadUrl { get; set; } = "";     // Time-limited SAS URL (1-hour expiry)
    public string Description { get; set; } = "";
    public string SourceDocument { get; set; } = "";
    public int PageNumber { get; set; }
}

// Token usage tracking for cost monitoring and observability.
// INTERVIEW TIP: "We track tokens per request so we can monitor cost trends,
// set alerts on budget thresholds, and optimize model routing."
public class TokenUsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCost { get; set; }
    public int ToolCallCount { get; set; }
}

// =====================================================================================
// AgentError — Tracks when a tool FAILS and whether we RECOVERED from it
// =====================================================================================
// Example: SQL query had a wrong column name → SqlQueryTool returned error →
// Orchestrator auto-retried with GetSchemaAsync first → second attempt succeeded.
// Frontend can display: "Answered from documents only — SQL was temporarily unavailable."
public class AgentError
{
    public string ToolName { get; set; } = "";    // Which tool failed (e.g., "QuerySqlAsync")
    public string ErrorType { get; set; } = "";   // "ToolFailure", "EmptyResult", "Timeout", "Degraded"
    public string Message { get; set; } = "";     // Human-readable error description
    public bool Recovered { get; set; }           // True = fallback succeeded, answer is still good
}

// =====================================================================================
// PiiSummary — PII redaction stats returned to the client (NEVER contains actual PII!)
// =====================================================================================
// The client gets: "3 PII items redacted (2 emails, 1 SSN)" — just COUNTS, not the values.
// The actual PII detection audit trail is logged internally for GDPR compliance only.
// INTERVIEW TIP: "We never return PII values in the response — only aggregate counts
// by type and pipeline layer, for transparency without compromising privacy."
public class PiiSummary
{
    public bool Enabled { get; set; }
    public int TotalRedactions { get; set; }
    public Dictionary<string, int> RedactionsByType { get; set; } = new();   // e.g., {"Email": 2, "SSN": 1}
    public Dictionary<string, int> RedactionsByLayer { get; set; } = new();  // e.g., {"UserInput": 1, "ToolResult": 2}
}
