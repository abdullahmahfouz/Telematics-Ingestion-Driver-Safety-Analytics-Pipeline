using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TelematicsPipeline.Api.Auth;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Auth;

public class JwtTokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-unit-test-signing-key";

    private static IConfiguration MakeConfiguration(string? signingKey = SigningKey) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = signingKey,
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:ExpiryMinutes"] = "60",
        })
        .Build();

    [Fact]
    public void IssueToken_ProducesATokenThatValidatesWithTheSameKey()
    {
        var service = new JwtTokenService(MakeConfiguration());

        var (token, _) = service.IssueToken("demo");

        // MapInboundClaims mirrors Program.cs: without it, "sub" is silently renamed to
        // ClaimTypes.NameIdentifier and this lookup would miss even though the token is valid.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        }, out _);

        Assert.Equal("demo", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Fact]
    public void IssueToken_SetsExpiryFromConfiguredMinutes()
    {
        var service = new JwtTokenService(MakeConfiguration());

        var (_, expiresAt) = service.IssueToken("demo");

        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void IssueToken_Throws_WhenSigningKeyIsNotConfigured()
    {
        var service = new JwtTokenService(MakeConfiguration(signingKey: null));

        Assert.Throws<InvalidOperationException>(() => service.IssueToken("demo"));
    }
}
