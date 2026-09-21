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

    /// <summary>
    /// Counts harsh-acceleration events (per <see cref="HarshAccelerationDetector"/>) for one device
    /// within a trailing lookback window.
    /// </summary>
    /// <param name="deviceId">Device to count events for.</param>
    /// <param name="sinceHours">Lookback window in hours; clamped to [1, 720] (30 days).</param>
    public async Task<int> GetHarshAccelerationEventCountAsync(string deviceId, int sinceHours)
    {
        sinceHours = Math.Clamp(sinceHours, 1, 24 * 30);
        var since = DateTimeOffset.UtcNow.AddHours(-sinceHours);

        return await db.TelematicsRecords
            .Where(r => r.DeviceId == deviceId
                && r.Timestamp >= since
                && r.AccelerationXG != null
                && r.AccelerationXG >= HarshAccelerationDetector.HarshAccelerationThresholdG)
            .CountAsync();
    }

    /// <summary>
    /// Distinct device IDs that have ever sent a reading, most-recently-active first -- lets
    /// the dashboard show what's actually available to look up instead of someone having to
    /// already know or guess a valid device ID.
    /// </summary>
    public async Task<List<string>> GetKnownDeviceIdsAsync()
    {
        return await db.TelematicsRecords
            .GroupBy(r => r.DeviceId)
            .Select(g => new { DeviceId = g.Key, LastSeen = g.Max(r => r.Timestamp) })
            .OrderByDescending(g => g.LastSeen)
            .Select(g => g.DeviceId)
            .ToListAsync();
    }

    /// <summary>
    /// All-time harsh-event totals per device, computed fresh from Postgres. Mirrors the
    /// per-reading accumulation done at ingest time (each of the three detectors that
    /// matches on a reading adds 1), so this can resync the Redis leaderboard -- the
    /// source of truth stays Postgres, Redis is just a queryable view over it.
    /// </summary>
    public async Task<Dictionary<string, int>> GetHarshEventCountsByDeviceAsync()
    {
        var braking = await db.TelematicsRecords
            .Where(r => r.AccelerationXG != null && r.AccelerationXG <= HarshBrakingDetector.HarshBrakingThresholdG)
            .GroupBy(r => r.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToListAsync();

        var cornering = await db.TelematicsRecords
            .Where(r => r.AccelerationYG != null && Math.Abs(r.AccelerationYG!.Value) >= HarshCorneringDetector.HarshCorneringThresholdG)
            .GroupBy(r => r.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToListAsync();

        var acceleration = await db.TelematicsRecords
            .Where(r => r.AccelerationXG != null && r.AccelerationXG >= HarshAccelerationDetector.HarshAccelerationThresholdG)
            .GroupBy(r => r.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToListAsync();

        var totals = new Dictionary<string, int>();
        foreach (var group in new[] { braking, cornering, acceleration })
        {
            foreach (var entry in group)
            {
                totals[entry.DeviceId] = totals.GetValueOrDefault(entry.DeviceId) + entry.Count;
            }
        }

        return totals;
    }
}
