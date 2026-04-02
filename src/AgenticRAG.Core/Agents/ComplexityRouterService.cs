// =====================================================================================
// ComplexityRouterService — DECIDES whether to use the CHEAP or EXPENSIVE LLM
// =====================================================================================
//
// WHAT IS THIS?
// After the planning phase (GPT-4o-mini picks and calls tools), we have tool results.
// Now we need to GENERATE the final answer. This service decides:
//   - Simple question? → Use GPT-4o-mini ($0.15/1M tokens) — cheap and fast
//   - Complex question? → Use GPT-4o ($2.50/1M tokens) — smarter but 17x more expensive
//
// HOW DOES IT DECIDE?
// It uses 4 RULES (no LLM call needed, zero cost):
//   Rule 1: If 2+ different tools were called → Complex (needs to synthesize multiple sources)
//   Rule 2: If tool results are large (>2000 tokens) → Complex (lots of data to analyze)
//   Rule 3: If question contains "compare", "analyze", "why" → Complex (reasoning needed)
//   Rule 4: If question starts with "what is", "define", "list" → Simple (factual lookup)
//
// WHY RULE-BASED INSTEAD OF LLM-BASED?
// An LLM-based router would add ~$0.001 per query for the routing decision itself.
// Rule-based costs $0. Upgrade to LLM-based only if rule accuracy drops below 85%.
//
// INTERVIEW TIP: "We use a complexity router to save costs — simple lookups go to the
// cheap model, only complex analysis hits the expensive model. This can cut LLM costs by 60%."
// =====================================================================================
namespace AgenticRAG.Core.Agents;

// Simple = use cheap model (GPT-4o-mini), Complex = use powerful model (GPT-4o)
public enum QueryComplexity { Simple, Complex }

public class ComplexityRouterService
{
    // Questions starting with these words are usually simple factual lookups
    private static readonly string[] SimplePatterns =
        ["what is", "define", "list", "show me", "get", "how many"];

    // Questions containing these words usually need deeper reasoning
    private static readonly string[] ComplexPatterns =
        ["compare", "analyze", "why", "how does", "trend", "affect",
         "relationship", "correlat", "versus", "difference between"];

    // Called AFTER tools have already run — we know what tools were used and how much data came back
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

        // Default: simple — most enterprise queries are single-source lookups
        return QueryComplexity.Simple;
    }
}
