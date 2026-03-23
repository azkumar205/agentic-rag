namespace AgenticRag.Api.Models;

public record RagQueryRequest(string Query, string SearchType = "Hybrid", int TopK = 5);

public record AgentQueryRequest(string Query, string ThreadId);

public record CreateThreadRequest(string? Title = null);

public record AddMessageRequest(string Content);

public record IngestionRequest(string ContainerName, DateTimeOffset? Since = null);

public record MemoryFactRequest(string UserId, string Fact);

public record MemoryPreferenceRequest(string UserId, string Key, string Value);
