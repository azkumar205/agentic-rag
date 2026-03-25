// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// AgentController — Single HTTP endpoint for the Agentic RAG API.
//
// POST /api/agent/ask  → Receives a question, delegates to AgentOrchestrator,
//                        returns answer with citations, tool usage, and metadata.
//
// Thin controller — all business logic lives in AgentOrchestrator.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using AgenticRAG.Core.Agents;
using AgenticRAG.Core.Models;
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

    /// <summary>
    /// Ask the Agentic RAG system a question.
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AgentResponse>> Ask([FromBody] AgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        _logger.LogInformation("Received question: {Question}", request.Question[..Math.Min(100, request.Question.Length)]);

        var response = await _orchestrator.ProcessAsync(request);
        return Ok(response);
    }
}
