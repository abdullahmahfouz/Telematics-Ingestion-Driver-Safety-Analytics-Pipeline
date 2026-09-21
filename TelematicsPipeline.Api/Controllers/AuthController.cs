using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelematicsPipeline.Api.Auth;
using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.Persistence;

namespace TelematicsPipeline.Api.Controllers;

public sealed record LoginRequest
{
    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed record LoginResponse
{
    public required string Token { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record RegisterDeviceRequest
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    public required string DeviceId { get; init; }
}

public sealed record RegisterDeviceResponse
{
    public required string DeviceId { get; init; }
    public required string ApiKey { get; init; }
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController(TelematicsDbContext db, JwtTokenService tokenService, LoginRateLimiter loginRateLimiter) : ControllerBase
{
    /// <summary>Dashboard operator login. Returns a bearer token used by both the API and the AI assistant.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // HttpContext is null when a controller is exercised directly in a unit test rather
        // than through the ASP.NET pipeline; "unknown" just means those calls share one bucket.
        var clientKey = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        var (allowed, retryAfter) = loginRateLimiter.TryAcquire(clientKey);
        if (!allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { detail = $"Too many login attempts. Try again in {Math.Ceiling(retryAfter.TotalSeconds):F0} seconds." });
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == request.Username);

        // Same generic message whether the username doesn't exist or the password is wrong,
        // so a caller can't use this endpoint to enumerate valid usernames.
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { detail = "Invalid username or password." });
        }

        var (token, expiresAt) = tokenService.IssueToken(user.Username);
        return Ok(new LoginResponse { Token = token, ExpiresAt = expiresAt });
    }

    /// <summary>
    /// Provisions a new device API key. Requires a logged-in operator -- mirrors how a fleet
    /// admin would onboard a new device/gateway from the dashboard, rather than open self-registration.
    /// The plaintext key is returned exactly once; only its hash is persisted.
    /// </summary>
    [HttpPost("devices")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (await db.DeviceApiKeys.AnyAsync(d => d.DeviceId == request.DeviceId))
        {
            return Conflict(new { detail = $"Device '{request.DeviceId}' already has an API key." });
        }

        var plaintextKey = DeviceApiKeyGenerator.Generate(request.DeviceId);
        db.DeviceApiKeys.Add(new DeviceApiKey
        {
            DeviceId = request.DeviceId,
            KeyHash = DeviceApiKeyGenerator.Hash(plaintextKey),
        });
        await db.SaveChangesAsync();

        return Ok(new RegisterDeviceResponse { DeviceId = request.DeviceId, ApiKey = plaintextKey });
    }
}
