/**
 * Mirrors TelematicsPipeline.Api/SafetyEngine/HarshBrakingDetector.cs and
 * HarshCorneringDetector.cs. The backend is the source of truth for persisted
 * event detection — these are used here only for immediate visual flagging in the table.
 */
export const HarshBrakingThresholdG = -0.4;
export const HarshCorneringThresholdG = 0.45;
