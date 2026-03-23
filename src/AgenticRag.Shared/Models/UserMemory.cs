namespace AgenticRag.Shared.Models;

/// <summary>
/// Represents persisted user preferences and important facts for memorization.
/// </summary>
public class UserMemory
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, string> Preferences { get; set; } = new();
    public List<string> ImportantFacts { get; set; } = new();
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
