using Microsoft.AspNetCore.Mvc;
using TelematicsPipeline.Api.Models;

namespace TelematicsPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TelematicsController : ControllerBase
{
    private readonly ILogger<TelematicsController> _logger;

    public TelematicsController(ILogger<TelematicsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Accepts a single telemetry reading from a device for ingestion.
    /// [ApiController] runs DataAnnotations validation on TelematicsRecord automatically
    /// and returns 400 with a ValidationProblemDetails body before this method runs.
    /// </summary>
    [HttpPost("ingest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Ingest([FromBody] TelematicsRecord record)
    {
        _logger.LogInformation(
            "Ingested {DeviceId} @ {Timestamp:O}: {SpeedKmh} km/h, idling={IsIdling}",
            record.DeviceId, record.Timestamp, record.SpeedKmh, record.IsIdling);

        return Accepted();
    }
}
