// =====================================================================================
// IntentClassifierService — CLASSIFIES user intent to route to the right prompt strategy
// =====================================================================================
//
// WHAT DOES THIS DO?
// Before the LLM starts planning, this service identifies WHAT the user is trying to do.
// Different intents need different system prompts, few-shot examples, and CoT strategies.
//
// 5 INTENT CATEGORIES:
// ┌─────────────────────┬──────────────────────────────────────────────────────────┐
// │ FactualLookup       │ "What is the SLA for Acme contract?"                    │
// │                     │ → Single tool, direct answer, citation-heavy            │
// │ ComparisonAnalysis  │ "Compare Q1 vs Q2 billing for Contoso"                  │
// │                     │ → Multi-tool, table format, GPT-4o for synthesis        │
// │ ProceduralHowTo     │ "How do I submit an invoice dispute?"                   │
// │                     │ → Step-by-step, doc search, numbered instructions       │
// │ DataRetrieval       │ "Show all invoices over $10,000 from last quarter"      │
// │                     │ → SQL-first, table format, exact numbers                │
// │ GeneralChitchat     │ "Hello", "Thanks", "What can you do?"                   │
// │                     │ → No tools needed, friendly response, suggest questions │
// └─────────────────────┴──────────────────────────────────────────────────────────┘
//
// IMPLEMENTATION: Rule-based (zero LLM cost) with keyword + pattern matching.
// Upgrade to LLM-based classification only if rule accuracy drops below 85%.
//
// INTERVIEW TIP: "We classify intent before prompting. Each intent gets a tailored
// system prompt with domain-specific few-shot examples and CoT reasoning structure.
// This improves answer quality by ~15% on our eval set compared to a single generic prompt."
// =====================================================================================
namespace AgenticRAG.Core.Agents;

/// <summary>
/// The detected intent category for a user question.
/// Each intent maps to a specific prompt template with tailored few-shot examples and CoT.
/// </summary>
public enum QueryIntent
{
    FactualLookup,        // Single-fact retrieval: "What is X?", "When does Y expire?"
    ComparisonAnalysis,   // Multi-item comparison or trend analysis
    ProceduralHowTo,      // Step-by-step process or procedure questions
    DataRetrieval,        // SQL-oriented data queries with specific filters
    GeneralChitchat       // Greetings, meta-questions, out-of-scope
}

/// <summary>
/// Result of intent classification with confidence and routing hints.
/// </summary>
public class IntentClassification
{
    public QueryIntent Intent { get; set; }
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = "";
}

public class IntentClassifierService
{
    // ── Factual lookups: short questions asking for a specific fact ──
    private static readonly string[] FactualPatterns =
        ["what is", "what are", "define", "who is", "when does", "when did",
         "where is", "which", "what's the", "tell me about", "explain"];

    // ── Comparison / analysis: need multi-source synthesis ──
    private static readonly string[] ComparisonPatterns =
        ["compare", "versus", "vs", "difference between", "how does .* differ",
         "analyze", "trend", "over time", "year over year", "month over month",
         "relationship between", "correlat", "impact of", "affect"];

    // ── Procedural: step-by-step how-to ──
    private static readonly string[] ProceduralPatterns =
        ["how do i", "how to", "steps to", "process for", "procedure",
         "guide me", "walk me through", "instructions for", "can i"];

    // ── Data retrieval: SQL-oriented, filter-heavy ──
    private static readonly string[] DataPatterns =
        ["show me", "list all", "get all", "how many", "total", "sum of",
         "invoices", "billing", "amount", "revenue", "cost", "count",
         "greater than", "less than", "last quarter", "last month",
         "top ", "highest", "lowest", "average", "between .* and"];

    // ── Chitchat: greetings, meta, out of scope ──
    private static readonly string[] ChitchatPatterns =
        ["hello", "hi ", "hey", "thanks", "thank you", "good morning",
         "good afternoon", "what can you do", "who are you", "help me",
         "bye", "goodbye"];

    /// <summary>
    /// Classifies user intent using rule-based pattern matching (zero LLM cost).
    /// Returns intent category + confidence score (0-1) + reasoning for observability.
    /// </summary>
    public IntentClassification Classify(string question)
    {
        var q = question.Trim().ToLowerInvariant();

        // Check chitchat FIRST — short greetings/meta should not trigger tools
        if (q.Split(' ').Length <= 4 && ChitchatPatterns.Any(p => q.Contains(p)))
        {
            return new IntentClassification
            {
                Intent = QueryIntent.GeneralChitchat,
                Confidence = 0.90,
                Reasoning = "Short greeting or meta-question detected"
            };
        }

        // Score each intent by counting pattern matches
        int comparisonScore = ComparisonPatterns.Count(p => q.Contains(p));
        int proceduralScore = ProceduralPatterns.Count(p => q.Contains(p));
        int dataScore = DataPatterns.Count(p => q.Contains(p));
        int factualScore = FactualPatterns.Count(p => q.StartsWith(p) || q.Contains(p));

        // Pick the highest-scoring intent
        var scores = new (QueryIntent intent, int score, string reason)[]
        {
            (QueryIntent.ComparisonAnalysis, comparisonScore, "Comparison/analysis keywords detected"),
            (QueryIntent.DataRetrieval, dataScore, "Data retrieval / SQL-oriented keywords detected"),
            (QueryIntent.ProceduralHowTo, proceduralScore, "Procedural/how-to keywords detected"),
            (QueryIntent.FactualLookup, factualScore, "Factual lookup keywords detected"),
        };

        var best = scores.OrderByDescending(s => s.score).First();

        if (best.score == 0)
        {
            // No strong signal — default to factual lookup (safest: will search docs)
            return new IntentClassification
            {
                Intent = QueryIntent.FactualLookup,
                Confidence = 0.50,
                Reasoning = "No strong intent signal — defaulting to factual lookup"
            };
        }

        // Confidence scales with number of matching patterns (capped at 0.95)
        double confidence = Math.Min(0.95, 0.60 + (best.score * 0.10));

        return new IntentClassification
        {
            Intent = best.intent,
            Confidence = confidence,
            Reasoning = best.reason
        };
    }
}
