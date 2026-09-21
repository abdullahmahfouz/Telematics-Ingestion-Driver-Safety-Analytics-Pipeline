using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using TelematicsPipeline.Api.Caching;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Caching;

/// <summary>
/// Runs against a real Redis on database 1 (the app uses 0), so these tests never touch
/// data the running app depends on. Using real Redis rather than a fake keeps the sorted-set
/// semantics honest -- ordering, score accumulation and range reads are the whole point here.
/// </summary>
public class SafetyLeaderboardTests : IAsyncLifetime
{
    private const string TestConnectionString = "localhost:6379,defaultDatabase=1,abortConnect=false";

    private IConnectionMultiplexer _redis = null!;
    private SafetyLeaderboard _leaderboard = null!;

    public async Task InitializeAsync()
    {
        _redis = await ConnectionMultiplexer.ConnectAsync(TestConnectionString);
        _leaderboard = new SafetyLeaderboard(_redis, NullLogger<SafetyLeaderboard>.Instance);
        await _leaderboard.ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await _leaderboard.ResetAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task StartsEmpty()
    {
        Assert.Empty(await _leaderboard.GetTopAsync(10));
    }

    [Fact]
    public async Task AccumulatesEventsPerDevice()
    {
        await _leaderboard.RecordHarshEventsAsync("device-a", 2);
        await _leaderboard.RecordHarshEventsAsync("device-a", 3);

        var entries = await _leaderboard.GetTopAsync(10);

        Assert.Single(entries);
        Assert.Equal("device-a", entries[0].DeviceId);
        Assert.Equal(5, entries[0].HarshEventCount);
    }

    [Fact]
    public async Task RanksWorstOffendersFirst()
    {
        await _leaderboard.RecordHarshEventsAsync("safe-driver", 1);
        await _leaderboard.RecordHarshEventsAsync("worst-driver", 9);
        await _leaderboard.RecordHarshEventsAsync("middling-driver", 4);

        var entries = await _leaderboard.GetTopAsync(10);

        Assert.Equal(["worst-driver", "middling-driver", "safe-driver"], entries.Select(e => e.DeviceId));
        Assert.Equal([1, 2, 3], entries.Select(e => e.Rank));
    }

    [Fact]
    public async Task RespectsTheRequestedLimit()
    {
        for (var i = 0; i < 12; i++)
        {
            await _leaderboard.RecordHarshEventsAsync($"device-{i:D2}", i + 1);
        }

        Assert.Equal(3, (await _leaderboard.GetTopAsync(3)).Count);
    }

    [Fact]
    public async Task ClampsAbsurdLimits()
    {
        await _leaderboard.RecordHarshEventsAsync("device-a", 1);

        Assert.Single(await _leaderboard.GetTopAsync(-5));
        Assert.Single(await _leaderboard.GetTopAsync(100_000));
    }

    [Fact]
    public async Task IgnoresNonPositiveEventCounts()
    {
        // A clean reading must not put a device on the board at all.
        await _leaderboard.RecordHarshEventsAsync("clean-driver", 0);

        Assert.Empty(await _leaderboard.GetTopAsync(10));
    }

    [Fact]
    public async Task ReportsTheRunningTotalOnRecord()
    {
        Assert.Equal(2, await _leaderboard.RecordHarshEventsAsync("device-a", 2));
        Assert.Equal(5, await _leaderboard.RecordHarshEventsAsync("device-a", 3));
    }

    [Fact]
    public async Task DegradesGracefullyWithoutRedis()
    {
        // The constructor takes a nullable multiplexer precisely so the API can start and
        // keep ingesting when Redis is down. Nothing here may throw.
        var offline = new SafetyLeaderboard(null, NullLogger<SafetyLeaderboard>.Instance);

        Assert.False(offline.IsAvailable);
        Assert.Null(await offline.RecordHarshEventsAsync("device-a", 3));
        Assert.Empty(await offline.GetTopAsync(10));
        await offline.ResetAsync();
    }

    [Fact]
    public async Task RebuildAsync_ReplacesExistingStateWithTheGivenCounts()
    {
        // Simulates recovering from a Redis restart: whatever was live before (here, stale
        // data for a device Postgres no longer agrees with) must not survive the rebuild.
        await _leaderboard.RecordHarshEventsAsync("stale-device", 99);

        await _leaderboard.RebuildAsync(new Dictionary<string, int>
        {
            ["device-a"] = 5,
            ["device-b"] = 2,
        });

        var entries = await _leaderboard.GetTopAsync(10);

        Assert.Equal(["device-a", "device-b"], entries.Select(e => e.DeviceId));
        Assert.DoesNotContain(entries, e => e.DeviceId == "stale-device");
    }

    [Fact]
    public async Task RebuildAsync_ClearsTheBoardWhenGivenNoCounts()
    {
        await _leaderboard.RecordHarshEventsAsync("device-a", 3);

        await _leaderboard.RebuildAsync(new Dictionary<string, int>());

        Assert.Empty(await _leaderboard.GetTopAsync(10));
    }

    [Fact]
    public async Task RebuildAsync_DegradesGracefullyWithoutRedis()
    {
        var offline = new SafetyLeaderboard(null, NullLogger<SafetyLeaderboard>.Instance);

        await offline.RebuildAsync(new Dictionary<string, int> { ["device-a"] = 5 });

        Assert.Empty(await offline.GetTopAsync(10));
    }
}
