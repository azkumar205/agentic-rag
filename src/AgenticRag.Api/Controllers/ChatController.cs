using Microsoft.AspNetCore.Mvc;
using AgenticRag.Agent.Interfaces;
using AgenticRag.Shared.Models;
using AgenticRag.Api.Models;

namespace AgenticRag.Api.Controllers;

/// <summary>
/// Chat threads controller: create, manage, reset threads with full history and context carry-over.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatThreadService _chatThreadService;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatThreadService chatThreadService,
        IAgentOrchestrator orchestrator,
        ILogger<ChatController> logger)
    {
        _chatThreadService = chatThreadService;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new chat thread with a unique ID.
    /// </summary>
    [HttpPost("threads")]
    [ProducesResponseType(typeof(ChatThread), 201)]
    public async Task<IActionResult> CreateThread(CancellationToken cancellationToken)
    {
        var thread = await _chatThreadService.CreateThreadAsync(cancellationToken);
        _logger.LogInformation("Created thread {ThreadId}", thread.Id);
        return CreatedAtAction(nameof(GetThread), new { threadId = thread.Id }, thread);
    }

    /// <summary>
    /// Gets a chat thread by ID including full message history.
    /// </summary>
    [HttpGet("threads/{threadId}")]
    [ProducesResponseType(typeof(ChatThread), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetThread(string threadId, CancellationToken cancellationToken)
    {
        try
        {
            var thread = await _chatThreadService.GetThreadAsync(threadId, cancellationToken);
            return Ok(thread);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Lists all chat threads.
    /// </summary>
    [HttpGet("threads")]
    [ProducesResponseType(typeof(IEnumerable<ChatThread>), 200)]
    public async Task<IActionResult> ListThreads(CancellationToken cancellationToken)
    {
        var threads = await _chatThreadService.ListThreadsAsync(cancellationToken);
        return Ok(threads);
    }

    /// <summary>
    /// Sends a message to an existing thread and gets an agentic response.
    /// </summary>
    [HttpPost("threads/{threadId}/messages")]
    [ProducesResponseType(typeof(AgentResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SendMessage(
        string threadId,
        [FromBody] AddMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _orchestrator.RunAsync(request.Content, threadId, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Resets a chat thread, clearing its message history while preserving the thread ID.
    /// </summary>
    [HttpPost("threads/{threadId}/reset")]
    [ProducesResponseType(typeof(ChatThread), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResetThread(string threadId, CancellationToken cancellationToken)
    {
        try
        {
            var thread = await _chatThreadService.ResetThreadAsync(threadId, cancellationToken);
            return Ok(thread);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
