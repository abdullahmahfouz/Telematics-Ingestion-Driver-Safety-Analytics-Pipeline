using System.ComponentModel.DataAnnotations;

namespace TelematicsPipeline.Api.Models;

/// <summary>A dashboard operator who can log in and provision device API keys.</summary>
public sealed class User
{
    public int Id { get; init; }

    [Required]
    [StringLength(40, MinimumLength = 3)]
    public required string Username { get; init; }

    [Required]
    public required string PasswordHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
