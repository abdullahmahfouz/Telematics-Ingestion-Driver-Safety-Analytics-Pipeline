using TelematicsPipeline.Api.Models;

namespace TelematicsPipeline.Api.SafetyEngine;

public static class HarshBrakingDetector
{
    /// <summary>
    /// Deceleration at or below this (in g) counts as a harsh-braking event.
    /// Matches the example threshold in Geotab's own MyGeotab Rule Conditions docs
    /// (support.geotab.com/help/mygeotab/groups-and-rules/rules/rule-conditions).
    /// </summary>
    public const double HarshBrakingThresholdG = -0.4;

    public static bool IsHarshBraking(TelematicsRecord record) =>
        record.AccelerationXG is double g && g <= HarshBrakingThresholdG;
}
