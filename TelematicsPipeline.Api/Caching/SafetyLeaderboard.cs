using StackExchange.Redis;

namespace TelematicsPipeline.Api.Caching;

public sealed record LeaderboardEntry(int Rank, string DeviceId, int HarshEventCount);

/// <summary>
/// Live ranking of devices by harsh-event count, backed by a Redis sorted set.
///
/// A sorted set is the right structure here rather than just another cache: recording an
/// event is an O(log N) ZINCRBY and reading the worst offenders is an O(log N + M) range
/// read, with Redis maintaining the ordering. The Postgres equivalent is a GROUP BY across
/// the whole readings table on every dashboard poll, which grows with the data.
///
/// Every operation fails soft. The leaderboard is derived data -- Postgres remains the
/// source of truth -- so Redis being unavailable must degrade this feature, never break
/// ingestion.
/// </summary>
public sealed class SafetyLeaderboard(IConnectionMultiplexer? redis, ILogger<SafetyLeaderboard> logger)
{
    private const string LeaderboardKey = "leaderboard:harsh-events";

    public bool IsAvailable => redis?.IsConnected ?? false;

    /// <summary>Records harsh events against a device and returns its new total, or null if unavailable.</summary>
    public async Task<double?> RecordHarshEventsAsync(string deviceId, int count = 1)
    {
        if (count <= 0 || redis is null)
        {
            return null;
        }

        try
        {
            return await redis.GetDatabase().SortedSetIncrementAsync(LeaderboardKey, deviceId, count);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Leaderboard update skipped for {DeviceId} -- Redis unavailable", deviceId);
            return null;
        }
    }

    /// <summary>Worst offenders first. Returns an empty list when Redis is unavailable.</summary>
    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int limit)
    {
        limit = Math.Clamp(limit, 1, 100);

        if (redis is null)
        {
            return [];
        }

        try
        {
            var entries = await redis.GetDatabase()
                .SortedSetRangeByRankWithScoresAsync(LeaderboardKey, 0, limit - 1, Order.Descending);

            return entries
                .Select((entry, index) => new LeaderboardEntry(
                    Rank: index + 1,
                    DeviceId: entry.Element.ToString(),
                    HarshEventCount: (int)entry.Score))
                .ToList();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Leaderboard read failed -- Redis unavailable");
            return [];
        }
    }

    public async Task ResetAsync()
    {
        if (redis is null)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().KeyDeleteAsync(LeaderboardKey);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Leaderboard reset failed -- Redis unavailable");
        }
    }
}
