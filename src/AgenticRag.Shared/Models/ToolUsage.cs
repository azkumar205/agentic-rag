namespace AgenticRag.Shared.Models;

/// <summary>
/// Records the usage of a tool during an agentic reasoning step.
/// </summary>
public class ToolUsage
{
    public string ToolName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public DateTimeOffset CalledAt { get; set; } = DateTimeOffset.UtcNow;
    public long DurationMs { get; set; }
}
