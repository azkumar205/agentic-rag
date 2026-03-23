using Microsoft.AspNetCore.Mvc;
using AgenticRag.DataAccess.Interfaces;
using AgenticRag.Shared.Models;
using AgenticRag.Api.Models;

namespace AgenticRag.Api.Controllers;

/// <summary>
/// Document ingestion controller: trigger incremental ingestion and monitor job status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly IDocumentIngestionService _ingestionService;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(IDocumentIngestionService ingestionService, ILogger<IngestionController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Triggers an incremental ingestion run. Optionally accepts a 'since' timestamp to limit scope.
    /// In production this would be triggered by a weekly Azure Function timer.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(IngestionJob), 200)]
    public async Task<IActionResult> RunIngestion([FromBody] IngestionRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ingestion triggered for container '{Container}' since {Since}",
            request.ContainerName, request.Since);

        var job = await _ingestionService.RunIncrementalIngestionAsync(
            request.ContainerName,
            request.Since,
            cancellationToken);

        return Ok(job);
    }

    /// <summary>
    /// Gets the status of a previously submitted ingestion job.
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    [ProducesResponseType(typeof(IngestionJob), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetJobStatus(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _ingestionService.GetJobStatusAsync(jobId, cancellationToken);
            return Ok(job);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
