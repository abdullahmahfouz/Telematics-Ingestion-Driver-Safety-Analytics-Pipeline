using TelematicsPipeline.Api.Models;

namespace TelematicsPipeline.Api.SafetyEngine;

public static class HarshCorneringDetector
{
    /// <summary>
    /// Lateral G-force at or above this magnitude (either direction) counts as harsh cornering.
    /// Matches the example threshold in Geotab's own MyGeotab Rule Conditions docs
    /// (support.geotab.com/help/mygeotab/groups-and-rules/rules/rule-conditions).
    /// </summary>
    public const double HarshCorneringThresholdG = 0.45;

    public static bool IsHarshCornering(TelematicsRecord record) =>
        record.AccelerationYG is double g && Math.Abs(g) >= HarshCorneringThresholdG;
}
