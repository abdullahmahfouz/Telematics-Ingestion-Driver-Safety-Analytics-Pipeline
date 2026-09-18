using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TelematicsPipeline.Api.Models;

/// <summary>
/// A single raw telemetry reading emitted by a GO device over the ingestion stream.
/// Mirrors the shape of a MyGeotab StatusData / LogRecord fan-out for one device at one instant.
/// </summary>
public sealed record TelematicsRecord
{
    /// <summary>Database-generated primary key. Ignored on incoming requests.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [Required]
    [StringLength(20, MinimumLength = 3)]
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; init; }

    /// <summary>UTC capture time as reported by the device, not server receipt time.</summary>
    [Required]
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [Range(-90, 90)]
    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [Range(0, 300)]
    [JsonPropertyName("speedKmh")]
    public double SpeedKmh { get; init; }

    [Range(0, 359.9)]
    [JsonPropertyName("headingDegrees")]
    public double HeadingDegrees { get; init; }

    [Range(0, 10000)]
    [JsonPropertyName("engineRpm")]
    public int? EngineRpm { get; init; }

    [Range(-40, 150)]
    [JsonPropertyName("engineCoolantTempC")]
    public double? EngineCoolantTempC { get; init; }

    [Range(0, 100)]
    [JsonPropertyName("fuelLevelPercent")]
    public double? FuelLevelPercent { get; init; }

    [Range(0, 2000000)]
    [JsonPropertyName("odometerKm")]
    public double OdometerKm { get; init; }

    [JsonPropertyName("isIdling")]
    public bool IsIdling { get; init; }

    [JsonPropertyName("isIgnitionOn")]
    public bool IsIgnitionOn { get; init; }

    /// <summary>Longitudinal G-force (+forward accel / -braking), from the device accelerometer.</summary>
    [Range(-8, 8)]
    [JsonPropertyName("accelerationXG")]
    public double? AccelerationXG { get; init; }

    /// <summary>Lateral G-force (+right / -left), used for harsh-cornering detection.</summary>
    [Range(-8, 8)]
    [JsonPropertyName("accelerationYG")]
    public double? AccelerationYG { get; init; }

    /// <summary>Vertical G-force, useful for pothole/impact detection.</summary>
    [Range(-8, 8)]
    [JsonPropertyName("accelerationZG")]
    public double? AccelerationZG { get; init; }
}
