namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents a response from the agentic RAG pipeline including tool usage and reasoning.
/// </summary>
public class AgentResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public List<ToolUsage> ToolsUsed { get; set; } = new();
    public string ReasoningTrace { get; set; } = string.Empty;
    public bool FromCache { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
