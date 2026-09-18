using TelematicsPipeline.Api.Models;
using TelematicsPipeline.Api.SafetyEngine;
using Xunit;

namespace TelematicsPipeline.Api.Tests.SafetyEngine;

public class HarshCorneringDetectorTests
{
    private static TelematicsRecord RecordWithLateralAcceleration(double? accelerationYG) => new()
    {
        DeviceId = "test-device",
        Timestamp = DateTimeOffset.UtcNow,
        AccelerationYG = accelerationYG
    };

    [Fact]
    public void ReturnsFalse_WhenAccelerationIsMissing()
    {
        var record = RecordWithLateralAcceleration(null);

        Assert.False(HarshCorneringDetector.IsHarshCornering(record));
    }

    [Theory]
    [InlineData(0.1, false)]
    [InlineData(0.44, false)]
    [InlineData(0.45, true)]
    [InlineData(0.7, true)]
    [InlineData(-0.44, false)]
    [InlineData(-0.45, true)]
    [InlineData(-0.7, true)]
    public void ClassifiesAcrossBothTurnDirections(double accelerationYG, bool expectedHarshCornering)
    {
        var record = RecordWithLateralAcceleration(accelerationYG);

        Assert.Equal(expectedHarshCornering, HarshCorneringDetector.IsHarshCornering(record));
    }
}
