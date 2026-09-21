using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TelematicsPipeline.Api.Auth;
using TelematicsPipeline.Api.Controllers;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Controllers;

/// <summary>
/// Runs against a real, dedicated Postgres database (telematics_pipeline_test), same as
/// TelematicsQueryServiceTests, rather than a fake in-memory provider or mocked DbContext.
/// </summary>
[Collection("Shared Postgres/Redis")]
public class AuthControllerTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=telematics_pipeline_test;Username=Abdullah";

    private TelematicsDbContext _db = null!;
    private AuthController _controller = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TelematicsDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new TelematicsDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"DeviceApiKeys\"");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\"");

        // A generous cap -- this class's own tests call Login a handful of times each and
        // must not trip the limiter; RateLimiting_* tests below build their own tightly-capped
        // controller instead of using this one.
        _controller = new AuthController(_db, new JwtTokenService(CreateTestJwtConfiguration()), new LoginRateLimiter());
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static IConfiguration CreateTestJwtConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "unit-test-signing-key-unit-test-signing-key",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
            })
            .Build();

    private async Task<User> SeedUserAsync(string username, string password)
    {
        var user = new User { Username = username, PasswordHash = PasswordHasher.Hash(password) };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Login_ReturnsAToken_ForCorrectCredentials()
    {
        await SeedUserAsync("demo", "DemoPass123!");

        var result = await _controller.Login(new LoginRequest { Username = "demo", Password = "DemoPass123!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.NotEmpty(response.Token);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForAWrongPassword()
    {
        await SeedUserAsync("demo", "DemoPass123!");

        var result = await _controller.Login(new LoginRequest { Username = "demo", Password = "wrong" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForAnUnknownUsername()
    {
        var result = await _controller.Login(new LoginRequest { Username = "nobody", Password = "whatever" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RegisterDevice_ReturnsAPlaintextKeyStartingWithTheDeviceId()
    {
        var result = await _controller.RegisterDevice(new RegisterDeviceRequest { DeviceId = "device-a" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegisterDeviceResponse>(ok.Value);
        Assert.StartsWith("dak_device-a_", response.ApiKey);
    }

    [Fact]
    public async Task RegisterDevice_OnlyStoresTheHash_NeverThePlaintextKey()
    {
        var result = await _controller.RegisterDevice(new RegisterDeviceRequest { DeviceId = "device-a" });
        var response = Assert.IsType<RegisterDeviceResponse>(Assert.IsType<OkObjectResult>(result).Value);

        var stored = await _db.DeviceApiKeys.SingleAsync(d => d.DeviceId == "device-a");

        Assert.NotEqual(response.ApiKey, stored.KeyHash);
        Assert.Equal(DeviceApiKeyGenerator.Hash(response.ApiKey), stored.KeyHash);
    }

    [Fact]
    public async Task RegisterDevice_ReturnsConflict_WhenTheDeviceAlreadyHasAKey()
    {
        await _controller.RegisterDevice(new RegisterDeviceRequest { DeviceId = "device-a" });

        var result = await _controller.RegisterDevice(new RegisterDeviceRequest { DeviceId = "device-a" });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsTooManyRequests_AfterExceedingTheAttemptCapFromOneSource()
    {
        await SeedUserAsync("demo", "DemoPass123!");
        var limitedController = new AuthController(
            _db, new JwtTokenService(CreateTestJwtConfiguration()), new LoginRateLimiter(maxAttempts: 2));

        await limitedController.Login(new LoginRequest { Username = "demo", Password = "wrong" });
        await limitedController.Login(new LoginRequest { Username = "demo", Password = "wrong" });
        // The third attempt is throttled even with the correct password -- the limiter counts
        // every attempt, not just failed ones, so a valid credential can't be used to bypass it.
        var result = await limitedController.Login(new LoginRequest { Username = "demo", Password = "DemoPass123!" });

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Login_TracksAttemptsSeparately_ByRemoteIp()
    {
        await SeedUserAsync("demo", "DemoPass123!");
        var limitedController = new AuthController(
            _db, new JwtTokenService(CreateTestJwtConfiguration()), new LoginRateLimiter(maxAttempts: 1))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1") },
                },
            },
        };

        await limitedController.Login(new LoginRequest { Username = "demo", Password = "wrong" });

        // A second source IP must not be punished by the first IP's attempt.
        limitedController.ControllerContext.HttpContext.Connection.RemoteIpAddress =
            System.Net.IPAddress.Parse("10.0.0.2");
        var result = await limitedController.Login(new LoginRequest { Username = "demo", Password = "DemoPass123!" });

        Assert.IsType<OkObjectResult>(result);
    }
}
