using System.Text.Json;
using AgenticRAG.Core.Configuration;
using AgenticRAG.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticRAG.Core.Ambiguity;

// =====================================================================================
// QueryRewriteService — Rewrites user queries for better retrieval (low-cost pre-step)
// =====================================================================================
//
// WHAT IT DOES:
// Converts short/vague user wording into a clearer retrieval query while preserving intent.
// Example: "invoice issue last month" -> "invoice discrepancies for the previous month"
//
// SAFETY DESIGN:
// - If disabled, returns original question unchanged
// - If model response is invalid, returns original question
// - Hard cap on rewritten length to avoid prompt bloat
// =====================================================================================
public class QueryRewriteService
{
    private readonly IChatClient _planningClient;
    private readonly QueryRewriteSettings _settings;
    private readonly ILogger<QueryRewriteService> _logger;

    public QueryRewriteService(
        [FromKeyedServices("planning")] IChatClient planningClient,
        QueryRewriteSettings settings,
        ILogger<QueryRewriteService> logger)
    {
        _planningClient = planningClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<QueryRewriteResult> RewriteAsync(string question)
    {
        var fallback = new QueryRewriteResult
        {
            OriginalQuestion = question,
            RewrittenQuestion = question,
            Applied = false,
            Confidence = 1.0,
            Strategy = "disabled-or-fallback"
        };

        if (!_settings.Enabled || string.IsNullOrWhiteSpace(question))
            return fallback;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                Rewrite the user query ONLY to improve retrieval clarity while preserving intent.
                Return strict JSON with fields: rewrittenQuestion, confidence, applied.
                Rules:
                - Do NOT add facts not present in user query.
                - Keep it concise.
                - If already clear, return original and applied=false.
                - confidence is 0..1.
                """),
            new(ChatRole.User, $"Question: {question}")
        };

        try
        {
            var response = await _planningClient.GetResponseAsync(messages, new ChatOptions { Temperature = 0.0f });
            var json = response.Text ?? "";

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var rewritten = root.TryGetProperty("rewrittenQuestion", out var rq)
                ? rq.GetString() ?? question
                : question;

            var confidence = root.TryGetProperty("confidence", out var c)
                ? c.GetDouble()
                : 0.5;

            var applied = root.TryGetProperty("applied", out var a)
                ? a.GetBoolean()
                : !string.Equals(rewritten, question, StringComparison.Ordinal);

            rewritten = rewritten.Trim();
            if (string.IsNullOrWhiteSpace(rewritten))
                rewritten = question;

            if (rewritten.Length > _settings.MaxRewriteChars)
                rewritten = rewritten[.._settings.MaxRewriteChars];

            if (!applied || confidence < _settings.MinRewriteConfidence)
            {
                return fallback;
            }

            _logger.LogInformation("Query rewrite applied (confidence={Confidence:F2})", confidence);

            return new QueryRewriteResult
            {
                OriginalQuestion = question,
                RewrittenQuestion = rewritten,
                Applied = true,
                Confidence = confidence,
                Strategy = "llm-rewrite"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query rewrite failed; using original question");
            return fallback;
        }
    }
}
