using System.ComponentModel.DataAnnotations;

namespace TelematicsPipeline.Api.Models;

/// <summary>
/// A hashed API key that authorizes ingestion requests for one device. The plaintext key is
/// shown to the caller exactly once, at creation time, and never stored.
/// </summary>
public sealed class DeviceApiKey
{
    public int Id { get; init; }

    [Required]
    [StringLength(20, MinimumLength = 3)]
    public required string DeviceId { get; init; }

    [Required]
    public required string KeyHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
