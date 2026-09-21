using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using TelematicsPipeline.Api.Auth;
using TelematicsPipeline.Api.Caching;
using TelematicsPipeline.Api.Controllers;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;
using TelematicsPipeline.Api.Services;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Controllers;

/// <summary>
/// Exercises TelematicsController.Ingest directly against a real Postgres test database and
/// real Redis (db 1) -- the same infrastructure the other integration tests use. The focus is
/// the controller's own device-scoping check: a device API key may only post under its own
/// DeviceId. This does not re-test the [Authorize] attribute itself -- that pipeline-level
/// enforcement (401 for a missing/invalid credential) is ASP.NET Core's own tested behavior,
/// not application logic written here.
/// </summary>
[Collection("Shared Postgres/Redis")]
public class TelematicsControllerTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=telematics_pipeline_test;Username=Abdullah";
    private const string TestRedisConnectionString = "localhost:6379,defaultDatabase=1,abortConnect=false";

    private TelematicsDbContext _db = null!;
    private IConnectionMultiplexer _redis = null!;
    private SafetyLeaderboard _leaderboard = null!;
    private TelematicsController _controller = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TelematicsDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new TelematicsDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"TelematicsRecords\"");

        _redis = await ConnectionMultiplexer.ConnectAsync(TestRedisConnectionString);
        _leaderboard = new SafetyLeaderboard(_redis, NullLogger<SafetyLeaderboard>.Instance);
        await _leaderboard.ResetAsync();

        _controller = new TelematicsController(
            NullLogger<TelematicsController>.Instance,
            _db,
            new TelematicsQueryService(_db),
            _leaderboard);
    }

    public async Task DisposeAsync()
    {
        await _leaderboard.ResetAsync();
        await _redis.DisposeAsync();
        await _db.DisposeAsync();
    }

    private static TelematicsRecord MakeRecord(string deviceId, double? accelerationXG = null) => new()
    {
        DeviceId = deviceId,
        Timestamp = DateTimeOffset.UtcNow,
        AccelerationXG = accelerationXG,
    };

    /// <summary>Simulates what ApiKeyAuthenticationHandler produces for a device API key.</summary>
    private void AuthenticateAsDevice(string deviceId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ApiKeyAuthenticationDefaults.DeviceIdClaimType, deviceId)],
            ApiKeyAuthenticationDefaults.Scheme);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    /// <summary>A JWT-authenticated dashboard user carries no device_id claim at all.</summary>
    private void AuthenticateAsDashboardUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
        };
    }

    [Fact]
    public async Task Ingest_DeviceApiKey_AcceptsWhenDeviceIdMatchesTheKey()
    {
        AuthenticateAsDevice("device-a");

        var result = await _controller.Ingest(MakeRecord("device-a"));

        Assert.IsType<AcceptedResult>(result);
        Assert.Single(await _db.TelematicsRecords.Where(r => r.DeviceId == "device-a").ToListAsync());
    }

    [Fact]
    public async Task Ingest_DeviceApiKey_ForbidsWhenDeviceIdDoesNotMatchTheKey()
    {
        // This is the one authorization rule stopping a device from posting data under a
        // different device's identity -- the highest-value thing in this controller to cover.
        AuthenticateAsDevice("device-a");

        var result = await _controller.Ingest(MakeRecord("device-b"));

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(await _db.TelematicsRecords.Where(r => r.DeviceId == "device-b").ToListAsync());
    }

    [Fact]
    public async Task Ingest_DashboardJwt_AcceptsAnyDeviceId()
    {
        // A logged-in human has no device_id claim to restrict against -- mirrors the UI's own
        // "send test reading" style usage, posting on behalf of whichever device is selected.
        AuthenticateAsDashboardUser();

        var result = await _controller.Ingest(MakeRecord("any-device"));

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task Ingest_HarshBraking_RecordsOnTheLiveLeaderboard()
    {
        AuthenticateAsDashboardUser();

        await _controller.Ingest(MakeRecord("device-c", accelerationXG: -0.62));

        var entries = await _leaderboard.GetTopAsync(10);
        Assert.Contains(entries, e => e.DeviceId == "device-c" && e.HarshEventCount == 1);
    }

    [Fact]
    public async Task Ingest_NormalReading_DoesNotAppearOnTheLeaderboard()
    {
        AuthenticateAsDashboardUser();

        await _controller.Ingest(MakeRecord("device-d", accelerationXG: 0.05));

        var entries = await _leaderboard.GetTopAsync(10);
        Assert.DoesNotContain(entries, e => e.DeviceId == "device-d");
    }
}
