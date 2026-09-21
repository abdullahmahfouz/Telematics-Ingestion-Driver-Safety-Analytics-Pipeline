using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TelematicsPipeline.Api.Auth;

/// <summary>Issues signed JWTs for dashboard users after they authenticate with a password.</summary>
public sealed class JwtTokenService(IConfiguration configuration)
{
    public static string SigningKeyOrThrow(IConfiguration configuration) =>
        configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException(
            "Jwt:SigningKey is not configured. Set it with `dotnet user-secrets set Jwt:SigningKey <value>` " +
            "in development, or the Jwt__SigningKey environment variable in other environments.");

    public (string Token, DateTimeOffset ExpiresAt) IssueToken(string username)
    {
        var signingKey = SigningKeyOrThrow(configuration);
        var issuer = configuration["Jwt:Issuer"] ?? "telematics-api";
        var audience = configuration["Jwt:Audience"] ?? "telematics-dashboard";
        var expiryMinutes = configuration.GetValue("Jwt:ExpiryMinutes", 120);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, username)],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
