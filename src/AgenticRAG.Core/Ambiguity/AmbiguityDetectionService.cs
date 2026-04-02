using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticRAG.Core.Configuration;
using AgenticRAG.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticRAG.Core.Ambiguity;

// =====================================================================================
// AmbiguityDetectionService — Detects whether a query is too vague to retrieve safely
// =====================================================================================
//
// WHY THIS MATTERS:
// Ambiguous queries cause poor retrieval and hallucinated assumptions.
// Clarify-first avoids expensive wrong tool calls and improves grounded answers.
//
// DESIGN:
// 1) LLM classifier (structured JSON)
// 2) Heuristic fallback (pronouns + underspecified patterns) if classifier fails
// =====================================================================================
public class AmbiguityDetectionService
{
    // Heuristic signal #1: pronouns without a clear referent often indicate missing context.
    // Example: "show that report" or "what about it" in a new session.
    private static readonly Regex VaguePronouns = new(@"\b(this|that|it|they|those|these|them)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Heuristic signal #2: broad business nouns that usually need qualifiers.
    // Example: "invoice issue" is incomplete without vendor/date/status.
    private static readonly Regex MissingScopeHints = new(@"\b(report|invoice|contract|policy|trend|issue|problem|status)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IChatClient _planningClient;
    private readonly AmbiguitySettings _settings;
    private readonly ILogger<AmbiguityDetectionService> _logger;

    public AmbiguityDetectionService(
        [FromKeyedServices("planning")] IChatClient planningClient,
        AmbiguitySettings settings,
        ILogger<AmbiguityDetectionService> logger)
    {
        _planningClient = planningClient;
        _settings = settings;
        _logger = logger;
    }

    // =====================================================================================
    // AnalyzeAsync — Main ambiguity classifier used by the orchestrator
    // =====================================================================================
    // FLOW:
    // 1) Respect feature flag (disabled => always clear)
    // 2) Ask GPT-4o-mini for structured JSON decision
    // 3) Parse safely with defaults (never crash pipeline on malformed JSON)
    // 4) If LLM fails, fallback to deterministic heuristic classification
    //
    // INTERVIEW TIP: "We use an LLM-first classifier for flexibility, but keep a
    // deterministic heuristic fallback so ambiguity routing never blocks the request path."
    // =====================================================================================
    public async Task<AmbiguityAnalysis> AnalyzeAsync(string question)
    {
        // Guardrail: if feature is off or question is empty, skip ambiguity logic entirely.
        // This keeps baseline behavior identical during staged rollout.
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(question))
        {
            return new AmbiguityAnalysis { IsAmbiguous = false, Confidence = 0.0, Reason = "disabled" };
        }

        // Ask for strict JSON so downstream parsing is predictable and low-risk.
        // Temperature 0 reduces classification variance across similar queries.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                Determine whether the query is ambiguous for enterprise retrieval.
                Return strict JSON with fields:
                isAmbiguous (bool), confidence (0..1), reason (string), ambiguousEntities (array).
                Each ambiguousEntities item: { name, prompt, suggestedOptions[] }.
                Mark ambiguous when key entity, timeframe, scope, or intent is missing.
                Keep to max 3 entities.
                """),
            new(ChatRole.User, $"Query: {question}")
        };

        try
        {
            var response = await _planningClient.GetResponseAsync(messages, new ChatOptions { Temperature = 0.0f });
            var json = response.Text ?? "";

            // Parse response defensively. Any missing fields get safe defaults.
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var analysis = new AmbiguityAnalysis
            {
                IsAmbiguous = root.TryGetProperty("isAmbiguous", out var amb) && amb.GetBoolean(),
                Confidence = root.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                Reason = root.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : ""
            };

            // Optional payload: which specific details are missing (entity/timeframe/scope).
            if (root.TryGetProperty("ambiguousEntities", out var entities) && entities.ValueKind == JsonValueKind.Array)
            {
                // Bound question count from config so UI payload remains compact.
                foreach (var item in entities.EnumerateArray().Take(_settings.MaxClarificationQuestions))
                {
                    var entity = new AmbiguousEntity
                    {
                        Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "detail" : "detail",
                        Prompt = item.TryGetProperty("prompt", out var p) ? p.GetString() ?? "Can you clarify?" : "Can you clarify?"
                    };

                    // Suggested options are optional. We keep at most 4 for concise UX chips.
                    if (item.TryGetProperty("suggestedOptions", out var opts) && opts.ValueKind == JsonValueKind.Array)
                    {
                        entity.SuggestedOptions = opts.EnumerateArray()
                            .Select(o => o.GetString() ?? "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Take(4)
                            .ToList();
                    }

                    analysis.AmbiguousEntities.Add(entity);
                }
            }

            return analysis;
        }
        catch (Exception ex)
        {
            // Never fail request processing because ambiguity classification failed.
            // Fall back to deterministic rules and continue safely.
            _logger.LogWarning(ex, "Ambiguity LLM classification failed; using heuristic fallback");
            return AnalyzeWithHeuristics(question);
        }
    }

    // =====================================================================================
    // AnalyzeWithHeuristics — deterministic fallback when LLM classification is unavailable
    // =====================================================================================
    // Heuristic decision:
    // ambiguous = (pronoun + broad noun) OR very short query (<= 4 words)
    //
    // WHY THIS RULE:
    // It is intentionally simple and conservative. It catches the most common vague forms
    // without adding extra model cost or introducing complex failure modes.
    // =====================================================================================
    private static AmbiguityAnalysis AnalyzeWithHeuristics(string question)
    {
        var words = question.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasPronouns = VaguePronouns.IsMatch(question);
        var hasBroadNoun = MissingScopeHints.IsMatch(question);
        var veryShort = words.Length <= 4;

        var ambiguous = (hasPronouns && hasBroadNoun) || veryShort;
        if (!ambiguous)
        {
            return new AmbiguityAnalysis
            {
                IsAmbiguous = false,
                Confidence = 0.35,
                Reason = "heuristic-clear"
            };
        }

        return new AmbiguityAnalysis
        {
            IsAmbiguous = true,
            Confidence = 0.8,
            Reason = "heuristic-underspecified",
            // Default clarification prompt if we don't have richer entity extraction.
            AmbiguousEntities = new List<AmbiguousEntity>
            {
                new()
                {
                    Name = "scope",
                    Prompt = "Can you specify the exact item and timeframe you mean?",
                    SuggestedOptions = new List<string> { "Last 30 days", "This quarter", "Specific document" }
                }
            }
        };
    }
}
