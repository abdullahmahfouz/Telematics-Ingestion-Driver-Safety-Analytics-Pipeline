using Microsoft.AspNetCore.Mvc;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.SafetyEngine;

namespace TelematicsPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TelematicsController : ControllerBase
{
    private readonly ILogger<TelematicsController> _logger;
    private readonly TelematicsDbContext _db;

    public TelematicsController(ILogger<TelematicsController> logger, TelematicsDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// Accepts a single telemetry reading from a device, persists it, and evaluates safety rules.
    /// [ApiController] runs DataAnnotations validation on TelematicsRecord automatically
    /// and returns 400 with a ValidationProblemDetails body before this method runs.
    /// </summary>
    [HttpPost("ingest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest([FromBody] TelematicsRecord record)
    {
        _db.TelematicsRecords.Add(record);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Ingested {DeviceId} @ {Timestamp:O}: {SpeedKmh} km/h, idling={IsIdling} (row id {Id})",
            record.DeviceId, record.Timestamp, record.SpeedKmh, record.IsIdling, record.Id);

        if (HarshBrakingDetector.IsHarshBraking(record))
        {
            _logger.LogWarning(
                "Harsh braking detected for {DeviceId} @ {Timestamp:O}: {AccelerationXG}g",
                record.DeviceId, record.Timestamp, record.AccelerationXG);
        }

        return Accepted(record);
    }
}
