// =====================================================================================
// AmbiguityModels.cs — Contracts for query rewriting and clarify-first responses
// =====================================================================================
//
// WHY THIS FILE EXISTS:
// These models describe two pre-retrieval controls:
// 1) Query rewriting metadata (what changed, confidence)
// 2) Clarification payload (what follow-up questions to ask when intent is ambiguous)
//
// INTERVIEW TIP: "Before expensive retrieval, we run a lightweight control layer:
// rewrite for clarity, then ambiguity detection. If ambiguity is high, we ask clarifying
// questions instead of guessing and hallucinating."
// =====================================================================================
namespace AgenticRAG.Core.Models;

public class QueryRewriteResult
{
    public string OriginalQuestion { get; set; } = "";
    public string RewrittenQuestion { get; set; } = "";
    public bool Applied { get; set; }
    public double Confidence { get; set; }
    public string Strategy { get; set; } = "none";
}

public class QueryRewriteInfo
{
    public string OriginalQuestion { get; set; } = "";
    public string EffectiveQuestion { get; set; } = "";
    public bool Applied { get; set; }
    public double Confidence { get; set; }
}

public class AmbiguityAnalysis
{
    public bool IsAmbiguous { get; set; }
    public double Confidence { get; set; }
    public string Reason { get; set; } = "";
    public List<AmbiguousEntity> AmbiguousEntities { get; set; } = new();
}

public class AmbiguousEntity
{
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public List<string> SuggestedOptions { get; set; } = new();
}

public class ClarificationRequest
{
    public string ClarificationId { get; set; } = "";
    public string Message { get; set; } = "";
    public bool AllowFreeText { get; set; } = true;
    public List<ClarificationQuestion> Questions { get; set; } = new();
}

public class ClarificationQuestion
{
    public string Field { get; set; } = "";
    public string Prompt { get; set; } = "";
    public List<string> SuggestedAnswers { get; set; } = new();
}
