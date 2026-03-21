using AgenticRag.Agents;
using AgenticRag.Cache;
using AgenticRag.Models;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Workflow;

/// <summary>
/// Agentic RAG Workflow — Orchestrates: Cache → Plan → Execute → Reflect → Re-plan loop.
/// This is the core orchestration engine that wires the three agents together.
/// </summary>
public sealed class RagWorkflow
{
    private readonly SemanticCache _cache;
    private readonly PlannerAgent _planner;
    private readonly ExecutorAgent _executor;
    private readonly ReflectionAgent _reflector;
    private readonly ILogger<RagWorkflow> _logger;

    private const int MaxReplanAttempts = 2;

    public RagWorkflow(
        SemanticCache cache,
        PlannerAgent planner,
        ExecutorAgent executor,
        ReflectionAgent reflector,
        ILogger<RagWorkflow> logger)
    {
        _cache = cache;
        _planner = planner;
        _executor = executor;
        _reflector = reflector;
        _logger = logger;
    }

    /// <summary>
    /// Full agentic RAG pipeline:
    ///   1. Semantic Cache check
    ///   2. Create execution plan (Planner Agent)
    ///   3. Execute plan steps (Executor Agent)
    ///   4. Evaluate evidence (Reflection Agent)
    ///   5. Re-plan if confidence &lt; 0.8 (max 2 re-attempts)
    ///   6. Cache and return answer
    /// </summary>
    public async Task<ChatResponse> AskAsync(string question, string userId)
    {
        // ── Step 1: Semantic Cache ───────────────────────
        var cached = await _cache.LookupAsync(question);
        if (cached is not null)
        {
            _logger.LogInformation("Cache HIT for question");
            return new ChatResponse(cached, FromCache: true, PlanSteps: 0,
                ReflectionAttempts: 0, ConfidenceScore: 1.0);
        }
        _logger.LogInformation("Cache MISS — starting agentic pipeline");

        // ── Step 2: Create Plan ──────────────────────────
        var plan = await _planner.CreatePlanAsync(question);
        _logger.LogInformation("Plan created with {StepCount} steps", plan.Steps.Count);

        // ── Step 3-5: Execute + Reflect Loop ─────────────
        string? finalAnswer = null;
        double confidence = 0;
        int reflectionAttempts = 0;

        for (int attempt = 0; attempt <= MaxReplanAttempts; attempt++)
        {
            reflectionAttempts = attempt + 1;

            // Execute
            var evidence = await _executor.ExecuteAsync(question, plan);
            _logger.LogInformation("Execution complete (attempt {Attempt})", attempt + 1);

            // Reflect
            var reflection = await _reflector.EvaluateAsync(question, evidence);
            confidence = reflection.ConfidenceScore;
            _logger.LogInformation("Reflection: confidence={Confidence}, complete={IsComplete}",
                reflection.ConfidenceScore, reflection.IsComplete);

            if (reflection.IsComplete && reflection.FinalAnswer is not null)
            {
                finalAnswer = reflection.FinalAnswer;
                break;
            }

            // Re-plan if we have attempts left
            if (attempt < MaxReplanAttempts && reflection.SuggestedAction is not null)
            {
                _logger.LogInformation("Re-planning — gap: {Gap}", reflection.GapAnalysis);
                var gapInfo = $"{reflection.GapAnalysis}. Suggested: {reflection.SuggestedAction}";
                plan = await _planner.CreatePlanAsync(question, gapInfo);
            }
        }

        finalAnswer ??= "Unable to find a satisfactory answer after multiple attempts.";

        // ── Step 6: Cache Result ─────────────────────────
        await _cache.StoreAsync(question, finalAnswer);
        _logger.LogInformation("Answer cached");

        return new ChatResponse(
            finalAnswer,
            FromCache: false,
            PlanSteps: plan.Steps.Count,
            ReflectionAttempts: reflectionAttempts,
            ConfidenceScore: confidence);
    }
}
