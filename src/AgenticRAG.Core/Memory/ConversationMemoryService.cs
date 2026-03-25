// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// ConversationMemoryService — Session-aware context for multi-turn chat.
//
// Classic RAG is stateless: every question starts fresh.
// This service stores conversation history per session in Redis, so the
// agent can resolve references like "that", "the same vendor", "compare
// with what we discussed earlier".
//
// Three memory levels:
//   Buffer   — Last N turns stored as-is (cheap, fast)
//   Summary  — Older turns LLM-summarized to save tokens when history grows
//   Persistent — Redis with 4-hour TTL per session
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.Text.Json;
using AgenticRAG.Core.Configuration;
using Microsoft.Extensions.AI;
using StackExchange.Redis;

namespace AgenticRAG.Core.Memory;

public class ConversationTurn
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class ConversationMemoryService
{
    private readonly IDatabase _redis;           // Redis database for session storage
    private readonly IChatClient _chatClient;    // Used only for summarizing long histories
    private readonly AgentSettings _settings;

    public ConversationMemoryService(
        IConnectionMultiplexer redis,
        IChatClient chatClient,
        AgentSettings settings)
    {
        _redis = redis.GetDatabase();
        _chatClient = chatClient;
        _settings = settings;
    }

    /// <summary>
    /// Loads conversation history for a session. If history exceeds SummarizeAfterTurns,
    /// older turns are LLM-summarized to keep token usage manageable.
    /// </summary>
    public async Task<List<ConversationTurn>> GetHistoryAsync(string sessionId)
    {
        var key = $"memory:{sessionId}";
        var data = await _redis.StringGetAsync(key);

        if (data.IsNullOrEmpty)
            return new List<ConversationTurn>();

        var turns = JsonSerializer.Deserialize<List<ConversationTurn>>(data!) ?? new();

        if (turns.Count > _settings.SummarizeAfterTurns)
        {
            var summary = await SummarizeHistoryAsync(turns);
            var summarizedTurns = new List<ConversationTurn>
            {
                new() { Role = "assistant", Content = $"[Conversation summary]: {summary}" }
            };
            summarizedTurns.AddRange(turns.TakeLast(2));
            return summarizedTurns;
        }

        return turns.TakeLast(_settings.MaxHistoryTurns).ToList();
    }

    /// Appends a turn (user message or assistant response) to the session history.
    /// Truncates content > 2000 chars, keeps max 20 turns, 4-hour TTL.
    public async Task AddTurnAsync(string sessionId, string role, string content)
    {
        var key = $"memory:{sessionId}";
        var data = await _redis.StringGetAsync(key);

        var turns = data.IsNullOrEmpty
            ? new List<ConversationTurn>()
            : JsonSerializer.Deserialize<List<ConversationTurn>>(data!) ?? new();

        turns.Add(new ConversationTurn
        {
            Role = role,
            Content = content.Length > 2000 ? content[..2000] + "..." : content,
            Timestamp = DateTimeOffset.UtcNow
        });

        if (turns.Count > 20) turns = turns.TakeLast(20).ToList();

        await _redis.StringSetAsync(key, JsonSerializer.Serialize(turns),
            TimeSpan.FromHours(4));
    }

    /// Compresses long conversation history into 2-3 sentences using GPT-4o.
    /// Keeps key topics, entities, and numbers so the agent doesn't lose context.
    private async Task<string> SummarizeHistoryAsync(List<ConversationTurn> turns)
    {
        var transcript = string.Join("\n", turns.Select(t =>
            $"{t.Role}: {(t.Content.Length > 300 ? t.Content[..300] + "..." : t.Content)}"));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Summarize this conversation in 2-3 sentences. Keep key topics, entities, conclusions, and any numbers."),
            new(ChatRole.User, transcript)
        };

        var result = await _chatClient.GetResponseAsync(messages);
        return result.Text ?? "Previous conversation context.";
    }
}
