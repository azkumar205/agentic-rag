using AgenticRAG.Core.Configuration;
using AgenticRAG.Core.Models;

namespace AgenticRAG.Core.Ambiguity;

// =====================================================================================
// ClarificationQuestionService — Builds user-facing clarification payloads
// =====================================================================================
//
// WHAT IT RETURNS:
// A structured payload the frontend can render directly:
// - Short message explaining why clarification is needed
// - One to N targeted follow-up questions
// - Optional suggested answers for quick clicks
// =====================================================================================
public class ClarificationQuestionService
{
    private readonly AmbiguitySettings _settings;

    public ClarificationQuestionService(AmbiguitySettings settings)
    {
        _settings = settings;
    }

    public ClarificationRequest BuildRequest(AmbiguityAnalysis analysis, string sessionId)
    {
        var questions = analysis.AmbiguousEntities
            .Take(_settings.MaxClarificationQuestions)
            .Select(e => new ClarificationQuestion
            {
                Field = e.Name,
                Prompt = e.Prompt,
                SuggestedAnswers = e.SuggestedOptions.Take(4).ToList()
            })
            .ToList();

        if (questions.Count == 0)
        {
            questions.Add(new ClarificationQuestion
            {
                Field = "intent",
                Prompt = "Can you clarify the exact detail you need and the timeframe?",
                SuggestedAnswers = new List<string> { "Last 30 days", "This quarter", "Specific customer/vendor" }
            });
        }

        return new ClarificationRequest
        {
            ClarificationId = $"{sessionId}-{Guid.NewGuid():N}"[..24],
            Message = "I can answer accurately once you clarify a few details.",
            AllowFreeText = _settings.AllowFreeText,
            Questions = questions
        };
    }
}
