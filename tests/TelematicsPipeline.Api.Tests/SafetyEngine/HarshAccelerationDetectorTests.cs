using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.SafetyEngine;
using Xunit;

namespace TelematicsPipeline.Api.Tests.SafetyEngine;

public class HarshAccelerationDetectorTests
{
    private static TelematicsRecord RecordWithAcceleration(double? accelerationXG) => new()
    {
        DeviceId = "test-device",
        Timestamp = DateTimeOffset.UtcNow,
        AccelerationXG = accelerationXG
    };

    [Fact]
    public void ReturnsFalse_WhenAccelerationIsMissing()
    {
        var record = RecordWithAcceleration(null);

        Assert.False(HarshAccelerationDetector.IsHarshAcceleration(record));
    }

    [Fact]
    public void ReturnsFalse_WhenBraking()
    {
        // Harsh braking (-0.62g) must never register as harsh acceleration -- they're opposite directions.
        var record = RecordWithAcceleration(-0.62);

        Assert.False(HarshAccelerationDetector.IsHarshAcceleration(record));
    }

    [Theory]
    [InlineData(0.05, false)]
    [InlineData(0.34, false)]
    [InlineData(0.35, true)]
    [InlineData(0.5, true)]
    public void ClassifiesAcrossARangeOfAccelerationValues(double accelerationXG, bool expectedHarshAcceleration)
    {
        var record = RecordWithAcceleration(accelerationXG);

        Assert.Equal(expectedHarshAcceleration, HarshAccelerationDetector.IsHarshAcceleration(record));
    }
}
