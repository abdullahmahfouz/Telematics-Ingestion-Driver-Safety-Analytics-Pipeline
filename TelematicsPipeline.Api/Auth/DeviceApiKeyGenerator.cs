using System.Security.Cryptography;
using System.Text;

namespace TelematicsPipeline.Api.Auth;

/// <summary>
/// Generates device API keys and hashes them for storage/lookup. Keys are high-entropy random
/// tokens (unlike user passwords), so a fast cryptographic hash is appropriate here -- there's
/// no brute-forceable low-entropy input to slow down, and a fast hash keeps ingestion cheap.
/// </summary>
public static class DeviceApiKeyGenerator
{
    /// <summary>Creates a new plaintext key. The caller must show it once and store only its hash.</summary>
    public static string Generate(string deviceId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"dak_{deviceId}_{token}";
    }

    public static string Hash(string plaintextKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey));
        return Convert.ToHexString(bytes);
    }
}
