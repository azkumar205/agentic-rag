// =====================================================================================
// AgenticRagMetrics.cs — CENTRALIZED METRICS for the entire Agentic RAG pipeline
// =====================================================================================
//
// WHAT IS THIS?
// A single class that defines ALL custom metrics the system emits. Every counter,
// histogram, and gauge lives here — ONE place to see everything the system measures.
//
// WHY System.Diagnostics.Metrics?
// .NET 8 has a built-in metrics API (System.Diagnostics.Metrics) that works with
// OpenTelemetry out of the box. We define metrics here (in Core), and the Api project
// wires them to Azure Monitor via OpenTelemetry. Zero extra NuGet packages in Core.
//
// HOW TO ADD A NEW METRIC:
//   1. Add a counter/histogram property below
//   2. Emit it in the service: AgenticRagMetrics.Instance.CacheHits.Add(1)
//   3. It automatically flows to Application Insights via OpenTelemetry
//
// METRIC NAMING CONVENTION: "agentic_rag.{subsystem}.{metric}"
// This follows OpenTelemetry semantic conventions for custom metrics.
//
// INTERVIEW TIP: "We centralize all metrics in one class so there's ONE place to audit
// what we measure. Each pipeline step emits metrics — cache, routing, reflection, tools,
// PII, cost. All flow to Azure Monitor via OpenTelemetry with zero extra packages in Core."
// =====================================================================================
using System.Diagnostics.Metrics;

namespace AgenticRAG.Core.Observability;

public class AgenticRagMetrics
{
    // The Meter is the "factory" for creating instruments. Its name ("AgenticRAG") must match
    // what we register in Program.cs: .AddMeter("AgenticRAG")
    public static readonly Meter Meter = new("AgenticRAG", "1.0.0");

    // ── CACHE METRICS ──
    // Tracks how often the semantic cache saves us from expensive LLM calls.
    // Dashboard: cache hit ratio = CacheHits / (CacheHits + CacheMisses)
    // Target: >30% hit rate means the cache is paying for itself.
    public static readonly Counter<long> CacheHits =
        Meter.CreateCounter<long>("agentic_rag.cache.hits", "count", "Semantic cache hits");

    public static readonly Counter<long> CacheMisses =
        Meter.CreateCounter<long>("agentic_rag.cache.misses", "count", "Semantic cache misses");

    // ── ROUTING METRICS ──
    // Tracks which model handles each request. If Complex/Simple ratio drifts,
    // it means question patterns changed — adjust routing thresholds.
    public static readonly Counter<long> RoutedSimple =
        Meter.CreateCounter<long>("agentic_rag.routing.simple", "count", "Queries routed to GPT-4o-mini");

    public static readonly Counter<long> RoutedComplex =
        Meter.CreateCounter<long>("agentic_rag.routing.complex", "count", "Queries routed to GPT-4o");

    // ── REFLECTION METRICS ──
    // The reflection score histogram shows answer quality distribution.
    // Alert if p50 drops below 6 — means systematic quality degradation.
    public static readonly Histogram<double> ReflectionScore =
        Meter.CreateHistogram<double>("agentic_rag.reflection.score", "score", "Reflection quality score (1-10)");

    public static readonly Counter<long> ReflectionRetries =
        Meter.CreateCounter<long>("agentic_rag.reflection.retries", "count", "Reflection-triggered retries");

    // ── LATENCY METRICS ──
    // End-to-end pipeline latency broken down by phase.
    // These histograms let you identify bottlenecks: is it tool calling? generation? reflection?
    public static readonly Histogram<double> PipelineLatencyMs =
        Meter.CreateHistogram<double>("agentic_rag.pipeline.latency_ms", "ms", "Total pipeline latency");

    public static readonly Histogram<double> PlanningLatencyMs =
        Meter.CreateHistogram<double>("agentic_rag.planning.latency_ms", "ms", "Planning phase latency");

    public static readonly Histogram<double> GenerationLatencyMs =
        Meter.CreateHistogram<double>("agentic_rag.generation.latency_ms", "ms", "Generation phase latency");

    // ── TOOL METRICS ──
    // Tracks tool call success, failure, and recovery rates.
    // High ToolErrors + high ToolRecoveries = resilient system.
    // High ToolErrors + low ToolRecoveries = broken system needs attention.
    public static readonly Counter<long> ToolCalls =
        Meter.CreateCounter<long>("agentic_rag.tools.calls", "count", "Total MCP tool calls");

    public static readonly Counter<long> ToolErrors =
        Meter.CreateCounter<long>("agentic_rag.tools.errors", "count", "Tool call failures");

    public static readonly Counter<long> ToolRecoveries =
        Meter.CreateCounter<long>("agentic_rag.tools.recoveries", "count", "Successful fallback recoveries");

    // ── COST METRICS ──
    // Estimated cost per request. Track daily/weekly totals in dashboards.
    // Alert if daily cost spikes >2x — could indicate infinite retry loops or abuse.
    public static readonly Histogram<double> EstimatedCostUsd =
        Meter.CreateHistogram<double>("agentic_rag.cost.estimated_usd", "USD", "Estimated cost per request");

    // ── INTENT METRICS ──
    // Tracks intent classification distribution. Useful for understanding traffic patterns.
    // If DataRetrieval suddenly spikes, SQL infra might need scaling.
    public static readonly Counter<long> IntentClassified =
        Meter.CreateCounter<long>("agentic_rag.intent.classified", "count", "Intent classifications");

    // ── PII METRICS ──
    // Tracks how much PII the system redacts. Spikes may indicate a new data source
    // with unmasked PII, or a prompt injection attempt trying to extract PII.
    public static readonly Counter<long> PiiRedactions =
        Meter.CreateCounter<long>("agentic_rag.pii.redactions", "count", "PII items redacted");

    // ── FEEDBACK METRICS ──
    // Tracks explicit user feedback (thumbs up/down). This is the ground truth signal
    // that validates whether our reflection scores correlate with real user satisfaction.
    public static readonly Counter<long> FeedbackPositive =
        Meter.CreateCounter<long>("agentic_rag.feedback.positive", "count", "Positive user feedback");

    public static readonly Counter<long> FeedbackNegative =
        Meter.CreateCounter<long>("agentic_rag.feedback.negative", "count", "Negative user feedback");

    // ── AMBIGUITY METRICS ──
    // Tracks how often the clarification-first path triggers.
    // High rate = users are asking vague questions → improve onboarding or suggested prompts.
    public static readonly Counter<long> ClarificationTriggered =
        Meter.CreateCounter<long>("agentic_rag.ambiguity.clarifications", "count", "Clarification requests sent");
}
