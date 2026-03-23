using AgenticRag.Agent.Interfaces;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Agent.Services;

/// <summary>
/// Manages chat threads with unique IDs, message history, context carry-over, and reset capability.
/// </summary>
public class ChatThreadService : IChatThreadService
{
    private readonly Dictionary<string, ChatThread> _threads = new();
    private readonly ILogger<ChatThreadService> _logger;

    public ChatThreadService(ILogger<ChatThreadService> logger)
    {
        _logger = logger;
    }

    public Task<ChatThread> CreateThreadAsync(CancellationToken cancellationToken = default)
    {
        var thread = new ChatThread
        {
            Title = $"Thread {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}"
        };
        _threads[thread.Id] = thread;
        _logger.LogInformation("Created chat thread '{ThreadId}'", thread.Id);
        return Task.FromResult(thread);
    }

    public Task<ChatThread> GetThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (!_threads.TryGetValue(threadId, out var thread))
        {
            throw new KeyNotFoundException($"Chat thread '{threadId}' not found.");
        }
        return Task.FromResult(thread);
    }

    public async Task<ChatThread> AddMessageAsync(string threadId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        var thread = await GetThreadAsync(threadId, cancellationToken);
        message.ThreadId = threadId;
        thread.Messages.Add(message);
        thread.LastUpdatedAt = DateTimeOffset.UtcNow;
        return thread;
    }

    public Task<ChatThread> ResetThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (!_threads.TryGetValue(threadId, out var thread))
        {
            throw new KeyNotFoundException($"Chat thread '{threadId}' not found.");
        }

        thread.Messages.Clear();
        thread.LastUpdatedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Reset chat thread '{ThreadId}'", threadId);
        return Task.FromResult(thread);
    }

    public Task<IEnumerable<ChatThread>> ListThreadsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<ChatThread>>(_threads.Values.OrderByDescending(t => t.LastUpdatedAt).ToList());
    }
}
