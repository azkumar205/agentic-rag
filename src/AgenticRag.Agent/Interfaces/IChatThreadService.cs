using AgenticRag.Shared.Models;

namespace AgenticRag.Agent.Interfaces;

/// <summary>
/// Manages chat threads with history, context carry-over, and reset options.
/// </summary>
public interface IChatThreadService
{
    Task<ChatThread> CreateThreadAsync(CancellationToken cancellationToken = default);
    Task<ChatThread> GetThreadAsync(string threadId, CancellationToken cancellationToken = default);
    Task<ChatThread> AddMessageAsync(string threadId, ChatMessage message, CancellationToken cancellationToken = default);
    Task<ChatThread> ResetThreadAsync(string threadId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChatThread>> ListThreadsAsync(CancellationToken cancellationToken = default);
}
