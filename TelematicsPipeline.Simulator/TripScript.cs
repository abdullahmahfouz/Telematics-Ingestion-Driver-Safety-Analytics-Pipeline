namespace TelematicsPipeline.Simulator;

public enum PhaseKind
{
    Idle,
    Accelerate,
    Cruise,
    Decelerate,
    HarshBraking,
    HarshAcceleration,
    HarshCornering,
}

/// <summary>One stretch of driving behaviour, held for <see cref="Seconds"/>.</summary>
/// <param name="Kind">What the driver is doing.</param>
/// <param name="Seconds">How long this phase lasts.</param>
/// <param name="TargetSpeedKmh">Speed the vehicle moves toward during this phase.</param>
public readonly record struct TripPhase(PhaseKind Kind, int Seconds, double TargetSpeedKmh);

/// <summary>
/// A scripted drive. Because the events are authored rather than random, the
/// expected outcome is known up front -- this trip should produce exactly
/// 2 harsh-braking, 1 harsh-acceleration and 1 harsh-cornering event, which is
/// what makes it useful for demoing and sanity-checking the detectors.
/// </summary>
public static class TripScript
{
    public static IReadOnlyList<TripPhase> RushHourCommute() =>
    [
        new(PhaseKind.Idle, 5, 0),
        new(PhaseKind.Accelerate, 12, 85),
        new(PhaseKind.Cruise, 20, 88),
        new(PhaseKind.HarshBraking, 2, 55),      // traffic ahead brakes suddenly
        new(PhaseKind.Cruise, 10, 58),
        new(PhaseKind.HarshAcceleration, 3, 90), // aggressive merge back up to speed
        new(PhaseKind.Cruise, 15, 92),
        new(PhaseKind.HarshCornering, 3, 70),    // takes the exit ramp too fast
        new(PhaseKind.Decelerate, 8, 40),
        new(PhaseKind.HarshBraking, 2, 10),      // late braking at the ramp light
        new(PhaseKind.Idle, 6, 0),
    ];

    /// <summary>
    /// Counts readings that will actually cross the backend's detector thresholds.
    /// Mirrors HarshBrakingDetector / HarshAccelerationDetector / HarshCorneringDetector,
    /// so the simulator can state up front what the API should report -- a harsh event is a
    /// momentary spike, so a 2-second phase typically produces one detection, not two.
    /// </summary>
    public static (int Braking, int Acceleration, int Cornering) ExpectedDetections(
        IEnumerable<SimulatedReading> readings)
    {
        const double brakingThresholdG = -0.4;
        const double accelerationThresholdG = 0.35;
        const double corneringThresholdG = 0.45;

        var braking = 0;
        var acceleration = 0;
        var cornering = 0;

        foreach (var reading in readings)
        {
            if (reading.AccelerationXG <= brakingThresholdG) braking++;
            if (reading.AccelerationXG >= accelerationThresholdG) acceleration++;
            if (Math.Abs(reading.AccelerationYG) >= corneringThresholdG) cornering++;
        }

        return (braking, acceleration, cornering);
    }
}
