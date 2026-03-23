using Microsoft.AspNetCore.Mvc;
using AgenticRag.Agent.Interfaces;
using AgenticRag.Shared.Models;
using AgenticRag.Api.Models;

namespace AgenticRag.Api.Controllers;

/// <summary>
/// Agentic RAG controller: multi-step reasoning with tool calling and multi-source retrieval.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ILogger<AgentController> _logger;

    public AgentController(IAgentOrchestrator orchestrator, ILogger<AgentController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Runs the agentic RAG pipeline with multi-step reasoning, tool chaining, and grounded answers.
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(AgentResponse), 200)]
    public async Task<IActionResult> Query([FromBody] AgentQueryRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent query: {Query} (thread: {ThreadId})", request.Query, request.ThreadId);
        var response = await _orchestrator.RunAsync(request.Query, request.ThreadId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Downloads the citation bundle from an agent response.
    /// </summary>
    [HttpPost("citations/download")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public IActionResult DownloadCitations([FromBody] AgentResponse agentResponse)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(agentResponse.Citations,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"agent-citations-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
    }
}
