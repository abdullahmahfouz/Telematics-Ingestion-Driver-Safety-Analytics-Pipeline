/**
 * Mirrors TelematicsPipeline.Api/SafetyEngine/HarshBrakingDetector.cs and
 * HarshCorneringDetector.cs. The backend is the source of truth for persisted
 * event detection — these are used here only for immediate visual flagging in the table.
 */
export const HarshBrakingThresholdG = -0.4;
export const HarshCorneringThresholdG = 0.45;
export const HarshAccelerationThresholdG = 0.35;

export function isHarshBraking(accelerationXG: number | null): boolean {
  return accelerationXG !== null && accelerationXG <= HarshBrakingThresholdG;
}

export function isHarshCornering(accelerationYG: number | null): boolean {
  return accelerationYG !== null && Math.abs(accelerationYG) >= HarshCorneringThresholdG;
}

export function isHarshAcceleration(accelerationXG: number | null): boolean {
  return accelerationXG !== null && accelerationXG >= HarshAccelerationThresholdG;
}
