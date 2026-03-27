// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// ComplexityRouterService — Routes queries to cheap or expensive LLM.
//
// After the planning phase (GPT-4o-mini selects and calls tools), this
// service decides whether the GENERATION step needs GPT-4o or can stay
// on GPT-4o-mini. Rule-based — no extra LLM cost for the routing decision.
//
// Routing logic:
//   Simple → GPT-4o-mini:  0-1 tools, short context, factual lookups
//   Complex → GPT-4o:      2+ tools, long context, analysis/comparison
//
// Why rule-based? At typical enterprise scale, a rule-based router saves
// $0 in LLM routing costs while an LLM-based router adds ~$0.001/query.
// Upgrade to LLM-based only if rule accuracy drops below 85%.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
namespace AgenticRAG.Core.Agents;

public enum QueryComplexity { Simple, Complex }

public class ComplexityRouterService
{
    private static readonly string[] SimplePatterns =
        ["what is", "define", "list", "show me", "get", "how many"];

    private static readonly string[] ComplexPatterns =
        ["compare", "analyze", "why", "how does", "trend", "affect",
         "relationship", "correlat", "versus", "difference between"];

    /// <summary>
    /// Classifies a query as Simple or Complex based on tool usage, context size, and question patterns.
    /// Called after planning phase — tools have already been selected and called.
    /// </summary>
    public QueryComplexity Classify(string question, List<string> toolsUsed, int contextTokenEstimate)
    {
        var q = question.ToLowerInvariant();

        // Rule 1: Multiple distinct tools = complex (multi-source synthesis needed)
        if (toolsUsed.Distinct().Count() >= 2)
            return QueryComplexity.Complex;

        // Rule 2: Large context = complex (lots of data to synthesize)
        if (contextTokenEstimate > 2000)
            return QueryComplexity.Complex;

        // Rule 3: Complex question patterns (analysis, comparison, reasoning)
        if (ComplexPatterns.Any(p => q.Contains(p)))
            return QueryComplexity.Complex;

        // Rule 4: Simple question patterns (factual lookups, definitions)
        if (SimplePatterns.Any(p => q.StartsWith(p)))
            return QueryComplexity.Simple;

        // Default: simple (majority of enterprise queries are single-source lookups)
        return QueryComplexity.Simple;
    }
}
