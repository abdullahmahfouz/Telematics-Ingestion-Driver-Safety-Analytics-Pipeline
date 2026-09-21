namespace TelematicsPipeline.Simulator;

public sealed record SimulatedReading(
    string DeviceId,
    DateTimeOffset Timestamp,
    double Latitude,
    double Longitude,
    double SpeedKmh,
    double HeadingDegrees,
    int EngineRpm,
    double EngineCoolantTempC,
    double FuelLevelPercent,
    double OdometerKm,
    bool IsIdling,
    bool IsIgnitionOn,
    double AccelerationXG,
    double AccelerationYG,
    double AccelerationZG,
    PhaseKind Phase);

/// <summary>
/// Plays a <see cref="TripScript"/> back at 1Hz, moving a vehicle along a
/// <see cref="Route"/> and emitting one reading per second.
///
/// Longitudinal G is derived from the actual second-to-second speed change rather
/// than stamped on, so the accelerometer values and the speed column always agree --
/// that internal consistency is what separates this from random noise.
/// </summary>
public sealed class TripSimulator(Route route, IReadOnlyList<TripPhase> phases, string deviceId, int? seed = null)
{
    private const double Gravity = 9.81;
    private const double IdleRpm = 750;

    private readonly Random _random = seed is null ? new Random() : new Random(seed.Value);

    public IEnumerable<SimulatedReading> Run(DateTimeOffset startTime, double startOdometerKm = 84_000)
    {
        var speedKmh = 0.0;
        var metresTravelled = 0.0;
        var odometerKm = startOdometerKm;
        var fuelPercent = 72.0;
        var coolantTempC = 24.0;
        var second = 0;
        var previousHeading = route.PositionAt(0).HeadingDegrees;

        foreach (var phase in phases)
        {
            for (var tick = 0; tick < phase.Seconds; tick++)
            {
                var previousSpeedKmh = speedKmh;
                speedKmh = NextSpeed(phase, speedKmh, tick);

                // Longitudinal G straight from the real speed delta over one second.
                var accelerationXG = (speedKmh - previousSpeedKmh) / 3.6 / Gravity;

                var (position, heading) = route.PositionAt(metresTravelled);

                // Lateral G from the actual yaw rate: a_lat = v * (Δheading/Δt).
                var headingDeltaRadians = ShortestAngleDelta(previousHeading, heading) * Math.PI / 180;
                var speedMetresPerSecond = speedKmh / 3.6;
                var accelerationYG = speedMetresPerSecond * headingDeltaRadians / Gravity;

                if (phase.Kind == PhaseKind.HarshCornering)
                {
                    // The corridor's own curvature is gentler than a hard exit-ramp turn,
                    // so the scripted event supplies the lateral force the route can't.
                    accelerationYG = 0.52 + _random.NextDouble() * 0.08;
                }

                previousHeading = heading;

                var isIdling = speedKmh < 1;
                metresTravelled += speedMetresPerSecond;
                odometerKm += speedMetresPerSecond / 1000;
                fuelPercent = Math.Max(0, fuelPercent - (isIdling ? 0.0006 : 0.0035));
                coolantTempC = Math.Min(92, coolantTempC + (coolantTempC < 90 ? 0.9 : 0.05));

                yield return new SimulatedReading(
                    DeviceId: deviceId,
                    Timestamp: startTime.AddSeconds(second),
                    Latitude: Math.Round(position.Latitude, 6),
                    Longitude: Math.Round(position.Longitude, 6),
                    SpeedKmh: Math.Round(speedKmh, 1),
                    HeadingDegrees: Math.Round(heading, 1),
                    EngineRpm: (int)Math.Round(isIdling ? IdleRpm : 900 + speedKmh * 19 + accelerationXG * 700),
                    EngineCoolantTempC: Math.Round(coolantTempC, 1),
                    FuelLevelPercent: Math.Round(fuelPercent, 2),
                    OdometerKm: Math.Round(odometerKm, 2),
                    IsIdling: isIdling,
                    IsIgnitionOn: true,
                    AccelerationXG: Math.Round(accelerationXG, 3),
                    AccelerationYG: Math.Round(accelerationYG, 3),
                    AccelerationZG: Math.Round(0.98 + _random.NextDouble() * 0.04, 3),
                    Phase: phase.Kind);

                second++;
            }
        }
    }

    /// <summary>
    /// Speed change a normal phase may apply in one second, in km/h. 0.25g is brisk
    /// but unremarkable driving -- deliberately under the 0.35g harsh-acceleration and
    /// 0.4g harsh-braking thresholds, so ordinary driving can never false-positive.
    /// Only the scripted harsh phases are allowed past this.
    /// </summary>
    private static readonly double NormalMaxDeltaKmh = 0.25 * Gravity * 3.6;

    private double NextSpeed(TripPhase phase, double currentSpeedKmh, int tick) => phase.Kind switch
    {
        // Coast down to a stop rather than snapping to zero. Slamming the speed to 0
        // would fabricate a braking spike proportional to whatever speed the vehicle
        // happened to be doing -- entering Idle straight from a cruise would invent a
        // harsh-braking event that the script never asked for.
        PhaseKind.Idle => currentSpeedKmh <= 1 ? 0 : ApproachCapped(currentSpeedKmh, 0, 1.0),

        // Harsh phases close most of the gap to the target in one second, which is
        // what pushes the derived G-force past the detector thresholds.
        PhaseKind.HarshBraking => Approach(currentSpeedKmh, phase.TargetSpeedKmh, 0.80),
        PhaseKind.HarshAcceleration => Approach(currentSpeedKmh, phase.TargetSpeedKmh, 0.55),

        PhaseKind.Accelerate => ApproachCapped(currentSpeedKmh, phase.TargetSpeedKmh, 0.22),
        PhaseKind.Decelerate => ApproachCapped(currentSpeedKmh, phase.TargetSpeedKmh, 0.20),
        PhaseKind.HarshCornering => ApproachCapped(currentSpeedKmh, phase.TargetSpeedKmh, 0.18),

        // Cruising drifts around the target the way real traffic does.
        PhaseKind.Cruise => Math.Max(0, ApproachCapped(currentSpeedKmh, phase.TargetSpeedKmh, 0.25)
                                        + (_random.NextDouble() - 0.5) * 1.2),

        _ => currentSpeedKmh,
    };

    private static double Approach(double current, double target, double rate) =>
        current + (target - current) * rate;

    private static double ApproachCapped(double current, double target, double rate)
    {
        var delta = (target - current) * rate;
        return current + Math.Clamp(delta, -NormalMaxDeltaKmh, NormalMaxDeltaKmh);
    }

    private static double ShortestAngleDelta(double fromDegrees, double toDegrees)
    {
        var delta = (toDegrees - fromDegrees + 540) % 360 - 180;
        return delta;
    }
}
