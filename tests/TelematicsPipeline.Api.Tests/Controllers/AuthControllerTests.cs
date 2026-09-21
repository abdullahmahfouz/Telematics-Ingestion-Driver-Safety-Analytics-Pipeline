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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "unit-test-signing-key-unit-test-signing-key",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
            })
            .Build();

        _controller = new AuthController(_db, new JwtTokenService(configuration));
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

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
}
