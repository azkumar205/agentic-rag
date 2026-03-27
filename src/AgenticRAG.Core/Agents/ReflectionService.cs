// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// ReflectionService — Self-correction layer (unique to Agentic RAG).
//
// After the agent generates an answer, this service asks a separate
// GPT-4o call to evaluate quality on 4 criteria: Grounded (1-3),
// Complete (1-3), Cited (1-2), Clear (1-2) = max 10.
// If score < threshold (default 6), the orchestrator retries with
// a refinement prompt. This catches ~30% of poor answers.
//
// Classic RAG has no self-awareness — bad search = bad answer, no recovery.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using Microsoft.Extensions.AI;

namespace AgenticRAG.Core.Agents;

public class ReflectionService
{
    private readonly IChatClient _chatClient;  // Uses a plain chat client (no tools) for evaluation

    public ReflectionService(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    /// <summary>
    /// Scores an answer 1-10 using a lightweight LLM call (~$0.002 per evaluation).
    /// Returns the integer score; orchestrator retries if below threshold.
    /// </summary>
    public async Task<int> EvaluateAsync(string question, string answer, List<string> toolsUsed)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are an answer quality evaluator. Score the answer from 1-10.

                Scoring criteria:
                - GROUNDED (1-3): Is the answer based on actual tool results, not made up?
                - COMPLETE (1-3): Does it address all parts of the question?
                - CITED (1-2): Are sources cited with [DocSource] or [SQLSource]?
                - CLEAR (1-2): Is the answer well-structured and easy to understand?

                Respond with ONLY a single integer 1-10. Nothing else.
                """),
            new(ChatRole.User, $"""
                Question: {question}

                Answer: {answer}

                Tools used: {string.Join(", ", toolsUsed)}

                Score (1-10):
                """)
        };

        var result = await _chatClient.GetResponseAsync(messages);
        if (int.TryParse(result.Text?.Trim(), out int score))
            return Math.Clamp(score, 1, 10);

        return 5;
    }
}
