using Microsoft.AspNetCore.Mvc;
using AgenticRag.Agent.Interfaces;
using AgenticRag.Shared.Models;
using AgenticRag.Api.Models;

namespace AgenticRag.Api.Controllers;

/// <summary>
/// Memory controller: manage user preferences and important facts for memorization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(IMemoryService memoryService, ILogger<MemoryController> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the memory for a user.
    /// </summary>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(UserMemory), 200)]
    public async Task<IActionResult> GetMemory(string userId, CancellationToken cancellationToken)
    {
        var memory = await _memoryService.GetMemoryAsync(userId, cancellationToken);
        return Ok(memory);
    }

    /// <summary>
    /// Adds an important fact to the user's memory.
    /// </summary>
    [HttpPost("facts")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> AddFact([FromBody] MemoryFactRequest request, CancellationToken cancellationToken)
    {
        await _memoryService.AddFactAsync(request.UserId, request.Fact, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Sets a user preference in memory.
    /// </summary>
    [HttpPost("preferences")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> SetPreference([FromBody] MemoryPreferenceRequest request, CancellationToken cancellationToken)
    {
        await _memoryService.SetPreferenceAsync(request.UserId, request.Key, request.Value, cancellationToken);
        return Ok();
    }
}
