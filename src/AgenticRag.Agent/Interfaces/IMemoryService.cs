using AgenticRag.Shared.Models;

namespace AgenticRag.Agent.Interfaces;

/// <summary>
/// Handles user memorization: persisting preferences and important facts across sessions.
/// </summary>
public interface IMemoryService
{
    Task<UserMemory> GetMemoryAsync(string userId, CancellationToken cancellationToken = default);
    Task UpsertMemoryAsync(string userId, UserMemory memory, CancellationToken cancellationToken = default);
    Task AddFactAsync(string userId, string fact, CancellationToken cancellationToken = default);
    Task SetPreferenceAsync(string userId, string key, string value, CancellationToken cancellationToken = default);
}
