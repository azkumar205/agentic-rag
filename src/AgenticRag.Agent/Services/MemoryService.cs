using AgenticRag.Agent.Interfaces;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Agent.Services;

/// <summary>
/// Persists user preferences and important facts for memorization across chat sessions.
/// Uses in-memory storage; in production this would be backed by a persistent store (e.g., Cosmos DB).
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly Dictionary<string, UserMemory> _store = new();
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(ILogger<MemoryService> logger)
    {
        _logger = logger;
    }

    public Task<UserMemory> GetMemoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(userId, out var memory))
        {
            memory = new UserMemory { UserId = userId };
            _store[userId] = memory;
        }
        return Task.FromResult(memory);
    }

    public Task UpsertMemoryAsync(string userId, UserMemory memory, CancellationToken cancellationToken = default)
    {
        memory.LastUpdatedAt = DateTimeOffset.UtcNow;
        _store[userId] = memory;
        _logger.LogInformation("Memory upserted for user '{UserId}'", userId);
        return Task.CompletedTask;
    }

    public async Task AddFactAsync(string userId, string fact, CancellationToken cancellationToken = default)
    {
        var memory = await GetMemoryAsync(userId, cancellationToken);
        if (!memory.ImportantFacts.Contains(fact))
        {
            memory.ImportantFacts.Add(fact);
            memory.LastUpdatedAt = DateTimeOffset.UtcNow;
            _logger.LogInformation("Added fact for user '{UserId}': {Fact}", userId, fact);
        }
    }

    public async Task SetPreferenceAsync(string userId, string key, string value, CancellationToken cancellationToken = default)
    {
        var memory = await GetMemoryAsync(userId, cancellationToken);
        memory.Preferences[key] = value;
        memory.LastUpdatedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Set preference '{Key}'='{Value}' for user '{UserId}'", key, value, userId);
    }
}
