using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TelematicsPipeline.Api.Persistence;

namespace TelematicsPipeline.Api.Auth;

public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string DeviceIdClaimType = "device_id";
}

/// <summary>
/// Authenticates ingestion requests from devices/gateways against the DeviceApiKeys table.
/// Separate from the JWT scheme used by human dashboard users -- a device is not a user.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TelematicsDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return AuthenticateResult.NoResult();
        }

        var keyHash = DeviceApiKeyGenerator.Hash(provided.ToString());
        var deviceKey = await db.DeviceApiKeys.AsNoTracking()
            .SingleOrDefaultAsync(k => k.KeyHash == keyHash);

        if (deviceKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new[] { new Claim(ApiKeyAuthenticationDefaults.DeviceIdClaimType, deviceKey.DeviceId) };
        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthenticationDefaults.Scheme);

        return AuthenticateResult.Success(ticket);
    }
}
