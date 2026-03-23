namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents a chat thread with history and context.
/// </summary>
public class ChatThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> UserPreferences { get; set; } = new();
    public List<string> ImportantFacts { get; set; } = new();
}
