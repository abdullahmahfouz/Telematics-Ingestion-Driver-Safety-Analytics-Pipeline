using Microsoft.EntityFrameworkCore;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.SafetyEngine;

namespace TelematicsPipeline.Api.Services;

/// <summary>
/// Read-only queries over persisted telematics data for the API's GET endpoints.
/// Keeps EF Core query logic out of the controller so it can be unit tested independently.
/// </summary>
public sealed class TelematicsQueryService(TelematicsDbContext db)
{
    /// <summary>
    /// Fetches the most recent readings for one device, newest first.
    /// </summary>
    /// <param name="deviceId">Device to look up readings for.</param>
    /// <param name="limit">Requested row count; clamped to [1, 100] to bound query cost.</param>
    public async Task<List<TelematicsRecord>> GetRecentRecordsAsync(string deviceId, int limit)
    {
        limit = Math.Clamp(limit, 1, 100);

        return await db.TelematicsRecords
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Counts harsh-braking events (per <see cref="HarshBrakingDetector"/>) for one device
    /// within a trailing lookback window.
    /// </summary>
    /// <param name="deviceId">Device to count events for.</param>
    /// <param name="sinceHours">Lookback window in hours; clamped to [1, 720] (30 days).</param>
    public async Task<int> GetHarshBrakingEventCountAsync(string deviceId, int sinceHours)
    {
        sinceHours = Math.Clamp(sinceHours, 1, 24 * 30);
        var since = DateTimeOffset.UtcNow.AddHours(-sinceHours);

        return await db.TelematicsRecords
            .Where(r => r.DeviceId == deviceId
                && r.Timestamp >= since
                && r.AccelerationXG != null
                && r.AccelerationXG <= HarshBrakingDetector.HarshBrakingThresholdG)
            .CountAsync();
    }

    /// <summary>
    /// Counts harsh-cornering events (per <see cref="HarshCorneringDetector"/>) for one device
    /// within a trailing lookback window.
    /// </summary>
    /// <param name="deviceId">Device to count events for.</param>
    /// <param name="sinceHours">Lookback window in hours; clamped to [1, 720] (30 days).</param>
    public async Task<int> GetHarshCorneringEventCountAsync(string deviceId, int sinceHours)
    {
        sinceHours = Math.Clamp(sinceHours, 1, 24 * 30);
        var since = DateTimeOffset.UtcNow.AddHours(-sinceHours);

        return await db.TelematicsRecords
            .Where(r => r.DeviceId == deviceId
                && r.Timestamp >= since
                && r.AccelerationYG != null
                && Math.Abs(r.AccelerationYG!.Value) >= HarshCorneringDetector.HarshCorneringThresholdG)
            .CountAsync();
    }
}
