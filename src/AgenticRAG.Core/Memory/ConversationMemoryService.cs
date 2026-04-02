// =====================================================================================
// ConversationMemoryService — MULTI-TURN CHAT: The agent remembers what you said before
// =====================================================================================
//
// WHAT IS THIS?
// Classic RAG is STATELESS — every question starts fresh with no memory. If you say
// "What's Contoso's contract value?" then follow up with "What about Fabrikam?", a
// stateless system has no idea what "What about" refers to.
//
// This service stores conversation history per session in Redis, so the agent can
// resolve references like "that", "the same vendor", "compare with what we discussed".
//
// THREE MEMORY LEVELS:
//   Buffer     — Last N turns stored as-is (cheap, fast, recent context)
//   Summary    — When history gets long (>SummarizeAfterTurns), older turns are
//                compressed into a 2-3 sentence summary by GPT-4o to save tokens
//   Persistent — Redis with 4-hour TTL per session (automatically cleaned up)
//
// HOW IT WORKS:
//   GetHistoryAsync(sessionId) → loads turns from Redis → summarizes if too long
//   AddTurnAsync(sessionId, role, content) → appends a turn → caps at 20 turns
//
// GRACEFUL DEGRADATION: Redis failure = agent answers without history (loses
// multi-turn context but still works). Memory is an OPTIMIZATION, not a requirement.
//
// INTERVIEW TIP: "We use Redis-backed conversation memory with automatic summarization.
// Short histories are kept as-is, long histories get LLM-compressed to save tokens."
// =====================================================================================
using System.Text.Json;
using AgenticRAG.Core.Configuration;
using Microsoft.Extensions.AI;
using StackExchange.Redis;

namespace AgenticRAG.Core.Memory;

// Represents one message in the conversation (user or assistant)
public class ConversationTurn
{
    public string Role { get; set; } = "";             // "user" or "assistant"
    public string Content { get; set; } = "";          // The message text
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class ConversationMemoryService
{
    private readonly IDatabase _redis;           // Redis database for per-session storage
    private readonly IChatClient _chatClient;    // Used ONLY for summarizing long histories
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

    // ── LOAD CONVERSATION HISTORY ──
    // Returns past turns for this session. If history is long, older turns get
    // LLM-summarized to keep token usage manageable (only recent 2 turns kept as-is).
    // Returns empty list on Redis failure — agent works without context.
    public async Task<List<ConversationTurn>> GetHistoryAsync(string sessionId)
    {
        try
        {
            var key = $"memory:{sessionId}";
            var data = await _redis.StringGetAsync(key);

            if (data.IsNullOrEmpty)
                return new List<ConversationTurn>();

            var turns = JsonSerializer.Deserialize<List<ConversationTurn>>(data!) ?? new();

            // If history is longer than SummarizeAfterTurns, compress old turns
            if (turns.Count > _settings.SummarizeAfterTurns)
            {
                var summary = await SummarizeHistoryAsync(turns);
                // Replace all old turns with a single summary + keep last 2 turns
                var summarizedTurns = new List<ConversationTurn>
                {
                    new() { Role = "assistant", Content = $"[Conversation summary]: {summary}" }
                };
                summarizedTurns.AddRange(turns.TakeLast(2));
                return summarizedTurns;
            }

            // History is short enough — return last MaxHistoryTurns as-is
            return turns.TakeLast(_settings.MaxHistoryTurns).ToList();
        }
        catch (Exception ex)
        {
            // Memory failure = agent answers without history (loses multi-turn context)
            Console.WriteLine($"[Memory] Failed to load history (continuing without context): {ex.Message}");
            return new List<ConversationTurn>();
        }
    }

    // ── SAVE A NEW TURN ──
    // Appends user message or assistant response to session history.
    // Truncates content > 2000 chars, keeps max 20 turns, 4-hour Redis TTL.
    // Failure is logged but NEVER prevents answer delivery.
    public async Task AddTurnAsync(string sessionId, string role, string content)
    {
        try
        {
            var key = $"memory:{sessionId}";
            var data = await _redis.StringGetAsync(key);

            var turns = data.IsNullOrEmpty
                ? new List<ConversationTurn>()
                : JsonSerializer.Deserialize<List<ConversationTurn>>(data!) ?? new();

            // Add the new turn (truncate long content to save Redis memory)
            turns.Add(new ConversationTurn
            {
                Role = role,
                Content = content.Length > 2000 ? content[..2000] + "..." : content,
                Timestamp = DateTimeOffset.UtcNow
            });

            // Cap at 20 turns to prevent unbounded growth
            if (turns.Count > 20) turns = turns.TakeLast(20).ToList();

            // Store with 4-hour TTL — sessions auto-expire after inactivity
            await _redis.StringSetAsync(key, JsonSerializer.Serialize(turns),
                TimeSpan.FromHours(4));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Memory] Failed to save turn (answer still delivered): {ex.Message}");
        }
    }

    // ── COMPRESS LONG HISTORY INTO A SUMMARY ──
    // Uses GPT-4o to condense many turns into 2-3 sentences.
    // Keeps key topics, entities, numbers so the agent doesn't lose important context.
    private async Task<string> SummarizeHistoryAsync(List<ConversationTurn> turns)
    {
        // Build a transcript, truncating each turn to 300 chars to fit in context window
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
