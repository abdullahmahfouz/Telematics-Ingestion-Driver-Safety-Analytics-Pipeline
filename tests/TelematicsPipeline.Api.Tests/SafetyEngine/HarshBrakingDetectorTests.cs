using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.SafetyEngine;
using Xunit;

namespace TelematicsPipeline.Api.Tests.SafetyEngine;

public class HarshBrakingDetectorTests
{
    private static TelematicsRecord RecordWithAcceleration(double? accelerationXG) => new()
    {
        DeviceId = "test-device",
        Timestamp = DateTimeOffset.UtcNow,
        AccelerationXG = accelerationXG
    };

    [Fact]
    public void ReturnsFalse_WhenAccelerationIsJustShortOfThreshold()
    {
        var record = RecordWithAcceleration(-0.39);

        Assert.False(HarshBrakingDetector.IsHarshBraking(record));
    }

    [Fact]
    public void ReturnsFalse_WhenAccelerationIsMissing()
    {
        var record = RecordWithAcceleration(null);

        Assert.False(HarshBrakingDetector.IsHarshBraking(record));
    }

    [Theory]
    [InlineData(0.02, false)]
    [InlineData(-0.1, false)]
    [InlineData(-0.4, true)]
    [InlineData(-0.62, true)]
    [InlineData(-1.5, true)]
    public void ClassifiesAcrossARangeOfAccelerationValues(double accelerationXG, bool expectedHarshBraking)
    {
        var record = RecordWithAcceleration(accelerationXG);

        Assert.Equal(expectedHarshBraking, HarshBrakingDetector.IsHarshBraking(record));
    }
}
