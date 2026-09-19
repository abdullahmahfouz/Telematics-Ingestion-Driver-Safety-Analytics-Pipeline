using Microsoft.EntityFrameworkCore;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.Services;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Services;

/// <summary>
/// Runs against a real, dedicated Postgres database (telematics_pipeline_test) rather than a
/// fake in-memory provider, so these tests exercise the same SQL translation path as production.
/// Each test clears the table first so tests never see each other's data.
/// </summary>
public class TelematicsQueryServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=telematics_pipeline_test;Username=Abdullah";

    private TelematicsDbContext _db = null!;
    private TelematicsQueryService _service = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TelematicsDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new TelematicsDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"TelematicsRecords\"");

        _service = new TelematicsQueryService(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static TelematicsRecord MakeRecord(
        string deviceId, DateTimeOffset timestamp, double? accelerationXG = null, double? accelerationYG = null) => new()
    {
        DeviceId = deviceId,
        Timestamp = timestamp,
        AccelerationXG = accelerationXG,
        AccelerationYG = accelerationYG
    };

    [Fact]
    public async Task GetRecentRecordsAsync_ReturnsNewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        _db.TelematicsRecords.AddRange(
            MakeRecord("device-a", now.AddMinutes(-10)),
            MakeRecord("device-a", now.AddMinutes(-5)),
            MakeRecord("device-a", now));
        await _db.SaveChangesAsync();

        var result = await _service.GetRecentRecordsAsync("device-a", limit: 10);

        Assert.Equal(3, result.Count);
        Assert.True(result[0].Timestamp > result[1].Timestamp);
        Assert.True(result[1].Timestamp > result[2].Timestamp);
    }

    [Fact]
    public async Task GetRecentRecordsAsync_RespectsLimit()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            _db.TelematicsRecords.Add(MakeRecord("device-b", now.AddMinutes(-i)));
        }
        await _db.SaveChangesAsync();

        var result = await _service.GetRecentRecordsAsync("device-b", limit: 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetRecentRecordsAsync_OnlyReturnsMatchingDevice()
    {
        var now = DateTimeOffset.UtcNow;
        _db.TelematicsRecords.Add(MakeRecord("device-c", now));
        _db.TelematicsRecords.Add(MakeRecord("other-device", now));
        await _db.SaveChangesAsync();

        var result = await _service.GetRecentRecordsAsync("device-c", limit: 10);

        Assert.Single(result);
        Assert.Equal("device-c", result[0].DeviceId);
    }

    [Fact]
    public async Task GetHarshBrakingEventCountAsync_CountsOnlyEventsPastThreshold()
    {
        var now = DateTimeOffset.UtcNow;
        _db.TelematicsRecords.AddRange(
            MakeRecord("device-d", now, accelerationXG: -0.62),
            MakeRecord("device-d", now, accelerationXG: -0.1),
            MakeRecord("device-d", now));
        await _db.SaveChangesAsync();

        var count = await _service.GetHarshBrakingEventCountAsync("device-d", sinceHours: 24);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetHarshBrakingEventCountAsync_ExcludesEventsOutsideWindow()
    {
        var now = DateTimeOffset.UtcNow;
        _db.TelematicsRecords.Add(MakeRecord("device-e", now.AddHours(-48), accelerationXG: -0.62));
        await _db.SaveChangesAsync();

        var count = await _service.GetHarshBrakingEventCountAsync("device-e", sinceHours: 24);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetHarshCorneringEventCountAsync_CountsBothDirections()
    {
        var now = DateTimeOffset.UtcNow;
        _db.TelematicsRecords.AddRange(
            MakeRecord("device-f", now, accelerationYG: -0.58),
            MakeRecord("device-f", now, accelerationYG: 0.58),
            MakeRecord("device-f", now, accelerationYG: 0.1));
        await _db.SaveChangesAsync();

        var count = await _service.GetHarshCorneringEventCountAsync("device-f", sinceHours: 24);

        Assert.Equal(2, count);
    }
}
