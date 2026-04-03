// =====================================================================================
// AgentController — THE ONLY REST ENDPOINT for the entire Agentic RAG system
// =====================================================================================
//
// WHAT IS THIS?
// The API has just ONE endpoint: POST /api/agent/ask
// The frontend sends a question → this controller hands it to the Orchestrator →
// and returns the answer with citations, tool usage, PII stats, and metadata.
//
// WHY ONLY ONE ENDPOINT?
// This is a "thin controller" pattern — the controller does ZERO business logic.
// All the intelligence (tool calling, caching, reflection, PII) lives in AgentOrchestrator.
// The controller just validates input and passes it through.
//
// ERROR HANDLING STRATEGY (Two Layers):
// Layer 1 (here): Catches agent pipeline failures → returns a friendly AgentResponse with error info
// Layer 2 (GlobalExceptionMiddleware): Catches truly unexpected crashes → returns RFC 7807 JSON
//
// INTERVIEW TIP: "We use a thin controller pattern — the controller only validates and delegates.
// All RAG logic is in the Orchestrator, making it testable without HTTP concerns."
// =====================================================================================
using AgenticRAG.Core.Agents;
using AgenticRAG.Core.Models;
using AgenticRAG.Core.Observability;
using Microsoft.AspNetCore.Mvc;

namespace AgenticRAG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<AgentController> _logger;

    public AgentController(AgentOrchestrator orchestrator, ILogger<AgentController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // POST /api/agent/ask — The single entry point for all questions
    // Request body: { "question": "...", "sessionId": "...", "topK": 5 }
    // Response: { "answer": "...", "textCitations": [...], "toolsUsed": [...], ... }
    [HttpPost("ask")]
    public async Task<ActionResult<AgentResponse>> Ask([FromBody] AgentRequest request)
    {
        // Basic validation — the question is the only required field
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        // Log only first 100 chars to avoid PII leaking into logs
        _logger.LogInformation("Received question: {Question}", request.Question[..Math.Min(100, request.Question.Length)]);

        try
        {
            // Hand off to the Orchestrator — this triggers the entire pipeline:
            // Cache check → Memory → Tool calling → Generation → Reflection → Cache → Memory
            var response = await _orchestrator.ProcessAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent pipeline failed for session {SessionId}", request.SessionId);

            // Return a structured error response instead of an ugly 500.
            // The frontend can display: "Something went wrong, please try again."
            // Meanwhile, the error is logged with session ID for debugging.
            return StatusCode(500, new AgentResponse
            {
                Answer = "I encountered an error processing your request. Please try again.",
                SessionId = request.SessionId ?? "",
                Errors = new List<AgentError>
                {
                    new()
                    {
                        ToolName = "AgentPipeline",
                        ErrorType = "PipelineFailure",
                        Message = "The agent pipeline encountered an unexpected error.",
                        Recovered = false
                    }
                }
            });
        }
    }

    // POST /api/agent/feedback — Record user thumbs up/down on an answer
    // This is the GROUND TRUTH signal for evaluating answer quality.
    // Metrics: positive/negative counters in Azure Monitor, correlated with reflection scores.
    // No PII: only SessionId (ephemeral), boolean rating, and optional sanitized comment.
    [HttpPost("feedback")]
    public ActionResult Feedback([FromBody] FeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest("SessionId is required.");

        if (request.IsPositive)
            AgenticRagMetrics.FeedbackPositive.Add(1);
        else
            AgenticRagMetrics.FeedbackNegative.Add(1);

        _logger.LogInformation("Feedback received: {Rating} for session {SessionId}",
            request.IsPositive ? "positive" : "negative", request.SessionId);

        return Ok(new { accepted = true });
    }
}
