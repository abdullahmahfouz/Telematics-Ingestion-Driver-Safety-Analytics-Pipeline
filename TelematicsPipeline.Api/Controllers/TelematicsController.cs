using Microsoft.AspNetCore.Mvc;
using TelematicsPipeline.Api.Caching;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.SafetyEngine;
using TelematicsPipeline.Api.Services;

namespace TelematicsPipeline.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TelematicsController(
    ILogger<TelematicsController> logger,
    TelematicsDbContext db,
    TelematicsQueryService queryService,
    SafetyLeaderboard leaderboard) : ControllerBase
{
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
        db.TelematicsRecords.Add(record);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Ingested {DeviceId} @ {Timestamp:O}: {SpeedKmh} km/h, idling={IsIdling} (row id {Id})",
            record.DeviceId, record.Timestamp, record.SpeedKmh, record.IsIdling, record.Id);

        var harshEvents = 0;

        if (HarshBrakingDetector.IsHarshBraking(record))
        {
            harshEvents++;
            logger.LogWarning(
                "Harsh braking detected for {DeviceId} @ {Timestamp:O}: {AccelerationXG}g",
                record.DeviceId, record.Timestamp, record.AccelerationXG);
        }

        if (HarshCorneringDetector.IsHarshCornering(record))
        {
            harshEvents++;
            logger.LogWarning(
                "Harsh cornering detected for {DeviceId} @ {Timestamp:O}: {AccelerationYG}g",
                record.DeviceId, record.Timestamp, record.AccelerationYG);
        }

        if (HarshAccelerationDetector.IsHarshAcceleration(record))
        {
            harshEvents++;
            logger.LogWarning(
                "Harsh acceleration detected for {DeviceId} @ {Timestamp:O}: {AccelerationXG}g",
                record.DeviceId, record.Timestamp, record.AccelerationXG);
        }

        // Deliberately after SaveChangesAsync: the row is already durable in Postgres, so a
        // Redis failure here costs a leaderboard increment, not the reading itself.
        await leaderboard.RecordHarshEventsAsync(record.DeviceId, harshEvents);

        return Accepted(record);
    }

    /// <summary>Most recent readings for one device, newest first.</summary>
    [HttpGet("{deviceId}/recent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent(string deviceId, [FromQuery] int limit = 10)
    {
        var records = await queryService.GetRecentRecordsAsync(deviceId, limit);
        return Ok(records);
    }

    /// <summary>Count of harsh-braking events for one device within a lookback window.</summary>
    [HttpGet("{deviceId}/harsh-braking-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHarshBrakingCount(string deviceId, [FromQuery] int sinceHours = 24)
    {
        var count = await queryService.GetHarshBrakingEventCountAsync(deviceId, sinceHours);
        return Ok(new { deviceId, sinceHours, harshBrakingEventCount = count });
    }

    /// <summary>Count of harsh-cornering events for one device within a lookback window.</summary>
    [HttpGet("{deviceId}/harsh-cornering-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHarshCorneringCount(string deviceId, [FromQuery] int sinceHours = 24)
    {
        var count = await queryService.GetHarshCorneringEventCountAsync(deviceId, sinceHours);
        return Ok(new { deviceId, sinceHours, harshCorneringEventCount = count });
    }

    /// <summary>Count of harsh-acceleration events for one device within a lookback window.</summary>
    [HttpGet("{deviceId}/harsh-acceleration-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHarshAccelerationCount(string deviceId, [FromQuery] int sinceHours = 24)
    {
        var count = await queryService.GetHarshAccelerationEventCountAsync(deviceId, sinceHours);
        return Ok(new { deviceId, sinceHours, harshAccelerationEventCount = count });
    }

    /// <summary>
    /// Live ranking of devices by harsh-event count, served from Redis.
    /// Returns an empty list (not an error) when Redis is unavailable.
    /// </summary>
    [HttpGet("leaderboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 10)
    {
        var entries = await leaderboard.GetTopAsync(limit);
        return Ok(new { available = leaderboard.IsAvailable, entries });
    }
}
