export interface TelematicsRecord {
  id: number;
  deviceId: string;
  timestamp: string;
  latitude: number;
  longitude: number;
  speedKmh: number;
  headingDegrees: number;
  engineRpm: number | null;
  engineCoolantTempC: number | null;
  fuelLevelPercent: number | null;
  odometerKm: number;
  isIdling: boolean;
  isIgnitionOn: boolean;
  accelerationXG: number | null;
  accelerationYG: number | null;
  accelerationZG: number | null;
}

export interface HarshEventCount {
  deviceId: string;
  sinceHours: number;
  harshBrakingEventCount?: number;
  harshCorneringEventCount?: number;
}
