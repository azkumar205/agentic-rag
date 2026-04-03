// =====================================================================================
// ReflectionService — SELF-CORRECTION: The agent checks its own answer quality
// =====================================================================================
//
// WHAT IS THIS?
// After the agent generates an answer, this service asks a SEPARATE LLM call to
// score the answer quality on a 1-10 scale. If the score is below threshold
// (default 6), the orchestrator retries with a refinement prompt.
//
// WHY IS THIS IMPORTANT?
// Classic RAG has NO self-awareness — if the search returns bad results, you get a
// bad answer and the system has no idea it's bad. This reflection step catches ~30%
// of poor answers and gives the agent a chance to fix them.
//
// HOW DOES SCORING WORK? (4 criteria, max 10 points):
//   - GROUNDED (1-3): Is the answer based on actual tool results, not hallucinated?
//   - COMPLETE (1-3): Does it address ALL parts of the question?
//   - CITED (1-2):    Are sources cited with [DocSource] or [SQLSource] tags?
//   - CLEAR (1-2):    Is the answer well-structured and easy to understand?
//
// COST: ~$0.002 per evaluation (very cheap — uses a lightweight LLM call)
//
// GRACEFUL DEGRADATION: If the reflection LLM call itself fails, we return a
// default score of 5 (pass) — we NEVER block answer delivery because of reflection.
//
// INTERVIEW TIP: "We use a reflection pattern — the agent evaluates its own output
// before returning it. This is what makes it 'agentic' vs. a simple RAG pipeline."
// =====================================================================================
using Microsoft.Extensions.AI;

namespace AgenticRAG.Core.Agents;

public class ReflectionService
{
    // Plain chat client (no tools attached) — only used for scoring, not tool calling
    private readonly IChatClient _chatClient;

    public ReflectionService(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    // Scores the answer 1-10. Orchestrator retries if score < threshold (default 6).
    // Returns default 5 on failure — reflection must NEVER block answer delivery.
    public async Task<int> EvaluateAsync(string question, string answer, List<string> toolsUsed)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                // System prompt tells the LLM to act as a quality evaluator
                new(ChatRole.System, """
                    You are an answer quality evaluator. Score the answer from 1-10.

                    Scoring criteria:
                    - GROUNDED (1-3): Is the answer based on actual tool results, not made up?
                    - COMPLETE (1-3): Does it address all parts of the question?
                    - CITED (1-2): Are sources cited with [DocSource] or [SQLSource]?
                    - CLEAR (1-2): Is the answer well-structured and easy to understand?

                    Respond with ONLY a single integer 1-10. Nothing else.
                    """),
                // User prompt provides the question, answer, and tools used for evaluation
                new(ChatRole.User, $"""
                    Question: {question}

                    Answer: {answer}

                    Tools used: {string.Join(", ", toolsUsed)}

                    Score (1-10):
                    """)
            };

            var result = await _chatClient.GetResponseAsync(messages);

            // Parse the LLM response as an integer 1-10
            if (int.TryParse(result.Text?.Trim(), out int score))
                return Math.Clamp(score, 1, 10);

            // If LLM returned something weird (not a number), default to 5 (pass)
            return 5;
        }
        catch (Exception ex)
        {
            // Reflection failure must NEVER prevent returning an answer to the user
            Console.WriteLine($"[Reflection] Evaluation failed (returning default score): {ex.Message}");
            return 5;
        }
    }
}
