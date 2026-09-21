using TelematicsPipeline.Api.Models;

namespace TelematicsPipeline.Api.SafetyEngine;

public static class HarshAccelerationDetector
{
    /// <summary>
    /// Forward acceleration at or above this (in g) counts as a harsh-acceleration event.
    /// Matches the example threshold in Geotab's own MyGeotab Rule Conditions docs
    /// (support.geotab.com/help/mygeotab/groups-and-rules/rules/rule-conditions).
    /// </summary>
    public const double HarshAccelerationThresholdG = 0.35;

    public static bool IsHarshAcceleration(TelematicsRecord record) =>
        record.AccelerationXG is double g && g >= HarshAccelerationThresholdG;
}
