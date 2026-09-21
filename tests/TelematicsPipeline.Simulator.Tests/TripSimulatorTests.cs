using TelematicsPipeline.Simulator;
using Xunit;

namespace TelematicsPipeline.Simulator.Tests;

/// <summary>
/// These lock in the physical realism of the generated data. Each of the first three
/// tests guards a bug that actually shipped into a dry run: normal driving reading as
/// a harsh event, curves producing no lateral force at all, and entering an idle phase
/// fabricating a braking spike out of the speed drop to zero.
/// </summary>
public class TripSimulatorTests
{
    // Mirrors the backend detectors in TelematicsPipeline.Api/SafetyEngine/*.cs.
    private const double HarshBrakingThresholdG = -0.4;
    private const double HarshAccelerationThresholdG = 0.35;
    private const double HarshCorneringThresholdG = 0.45;

    private static List<SimulatedReading> Run(IReadOnlyList<TripPhase> phases) =>
        new TripSimulator(Route.DonValleyParkway(), phases, "TEST-DEVICE", seed: 1)
            .Run(DateTimeOffset.UtcNow)
            .ToList();

    private static bool IsHarshPhase(PhaseKind kind) =>
        kind is PhaseKind.HarshBraking or PhaseKind.HarshAcceleration or PhaseKind.HarshCornering;

    [Fact]
    public void NormalDrivingNeverCrossesTheHarshThresholds()
    {
        var readings = Run(TripScript.RushHourCommute()).Where(r => !IsHarshPhase(r.Phase)).ToList();

        Assert.NotEmpty(readings);
        Assert.All(readings, r =>
        {
            Assert.True(
                r.AccelerationXG > HarshBrakingThresholdG,
                $"{r.Phase} at {r.SpeedKmh} km/h produced {r.AccelerationXG}g, which reads as harsh braking");
            Assert.True(
                r.AccelerationXG < HarshAccelerationThresholdG,
                $"{r.Phase} at {r.SpeedKmh} km/h produced {r.AccelerationXG}g, which reads as harsh acceleration");
            Assert.True(
                Math.Abs(r.AccelerationYG) < HarshCorneringThresholdG,
                $"{r.Phase} produced {r.AccelerationYG}g lateral, which reads as harsh cornering");
        });
    }

    [Fact]
    public void PullingAwayFromAStandstillIsNotHarshAcceleration()
    {
        var readings = Run([
            new TripPhase(PhaseKind.Idle, 3, 0),
            new TripPhase(PhaseKind.Accelerate, 10, 90),
        ]);

        Assert.All(readings, r => Assert.True(
            r.AccelerationXG < HarshAccelerationThresholdG,
            $"accelerating from rest produced {r.AccelerationXG}g"));
    }

    [Fact]
    public void ComingToAStopFromCruiseDoesNotFabricateAHarshBrakingEvent()
    {
        // The idle phase must coast down, not snap to zero -- snapping would invent a
        // braking spike proportional to whatever speed the vehicle was carrying.
        var readings = Run([
            new TripPhase(PhaseKind.Cruise, 6, 90),
            new TripPhase(PhaseKind.Idle, 10, 0),
        ]);

        Assert.All(readings, r => Assert.True(
            r.AccelerationXG > HarshBrakingThresholdG,
            $"{r.Phase} at {r.SpeedKmh} km/h produced {r.AccelerationXG}g"));

        Assert.Equal(0, readings[^1].SpeedKmh);
        Assert.True(readings[^1].IsIdling);
    }

    [Fact]
    public void DrivingThroughCurvesProducesRealLateralForce()
    {
        // Guards the bug where heading came from the current segment's bearing, so it only
        // changed at waypoints -- yaw rate was zero almost everywhere and every curve
        // reported 0.00g lateral.
        var readings = Run([
            new TripPhase(PhaseKind.Accelerate, 10, 80),
            new TripPhase(PhaseKind.Cruise, 50, 80),
        ]);

        var maxLateral = readings.Max(r => Math.Abs(r.AccelerationYG));

        Assert.True(maxLateral > 0.02, $"curves produced almost no lateral force (max {maxLateral}g)");
        Assert.True(maxLateral < HarshCorneringThresholdG, $"gentle curves read as harsh cornering ({maxLateral}g)");
    }

    [Fact]
    public void LongitudinalGMatchesTheActualSpeedChange()
    {
        // The accelerometer column and the speed column must tell the same story --
        // that internal consistency is what separates this from random noise.
        var readings = Run(TripScript.RushHourCommute());

        for (var i = 1; i < readings.Count; i++)
        {
            var expectedG = (readings[i].SpeedKmh - readings[i - 1].SpeedKmh) / 3.6 / 9.81;

            Assert.True(
                Math.Abs(readings[i].AccelerationXG - expectedG) < 0.02,
                $"reading {i}: reported {readings[i].AccelerationXG}g but the speed delta implies {expectedG:F3}g");
        }
    }

    [Fact]
    public void ScriptedHarshPhasesActuallyTripTheDetectors()
    {
        var readings = Run(TripScript.RushHourCommute());
        var (braking, acceleration, cornering) = TripScript.ExpectedDetections(readings);

        Assert.True(braking > 0, "the scripted trip produced no harsh-braking detections");
        Assert.True(acceleration > 0, "the scripted trip produced no harsh-acceleration detections");
        Assert.True(cornering > 0, "the scripted trip produced no harsh-cornering detections");
    }

    [Fact]
    public void RunIsRepeatableForAGivenSeed()
    {
        var first = Run(TripScript.RushHourCommute());
        var second = Run(TripScript.RushHourCommute());

        Assert.Equal(first.Select(r => r.SpeedKmh), second.Select(r => r.SpeedKmh));
    }

    [Fact]
    public void EmitsOneReadingPerScriptedSecond()
    {
        var phases = TripScript.RushHourCommute();

        Assert.Equal(phases.Sum(p => p.Seconds), Run(phases).Count);
    }

    [Fact]
    public void OdometerAndTimestampsAdvanceMonotonically()
    {
        var readings = Run(TripScript.RushHourCommute());

        for (var i = 1; i < readings.Count; i++)
        {
            Assert.True(readings[i].OdometerKm >= readings[i - 1].OdometerKm);
            Assert.True(readings[i].Timestamp > readings[i - 1].Timestamp);
        }
    }
}
