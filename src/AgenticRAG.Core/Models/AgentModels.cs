// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Agent Models — Request/Response contracts for the Agentic RAG API.
//
// AgentRequest:  What the client sends (question + optional session/filters)
// AgentResponse: What the API returns (answer + citations + tools + metadata)
// TextCitation:  Reference to a document chunk or SQL result used in the answer
// ImageCitation: Downloadable image from a document (SAS URL, 1-hour expiry)
// TokenUsageInfo: Cost tracking for observability
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
namespace AgenticRAG.Core.Models;

public class AgentRequest
{
    public string Question { get; set; } = "";
    public string? SessionId { get; set; }
    public string? Category { get; set; }
    public int TopK { get; set; } = 5;
}

public class AgentResponse
{
    public string Answer { get; set; } = "";
    public List<TextCitation> TextCitations { get; set; } = new();
    public List<ImageCitation> ImageCitations { get; set; } = new();
    public List<string> ToolsUsed { get; set; } = new();
    public List<string> ReasoningSteps { get; set; } = new();
    public int ReflectionScore { get; set; }
    public bool FromCache { get; set; }
    public TokenUsageInfo TokenUsage { get; set; } = new();
    public string SessionId { get; set; } = "";
}

public class TextCitation
{
    public int Index { get; set; }
    public string SourceDocument { get; set; } = "";
    public string Content { get; set; } = "";
    public double RelevanceScore { get; set; }
    public string SourceType { get; set; } = "document";
}

public class ImageCitation
{
    public int Index { get; set; }
    public string FileName { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceDocument { get; set; } = "";
    public int PageNumber { get; set; }
}

public class TokenUsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCost { get; set; }
    public int ToolCallCount { get; set; }
}
