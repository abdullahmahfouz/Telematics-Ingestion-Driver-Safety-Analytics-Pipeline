import type { HarshEventCount, TelematicsRecord } from "../types/telematics";
import { authHeaders, requireSession } from "./authToken";

const API_BASE_URL = "http://localhost:5231/api/telematics";

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { headers: authHeaders() });
  requireSession(response);
  if (!response.ok) {
    throw new Error(`Request to ${url} failed with status ${response.status}`);
  }
  return response.json() as Promise<T>;
}

/** Device IDs that have ever sent a reading, most-recently-active first. */
export function getDevices(): Promise<string[]> {
  return getJson(`${API_BASE_URL}/devices`);
}

export function getRecentRecords(deviceId: string, limit = 10): Promise<TelematicsRecord[]> {
  return getJson(`${API_BASE_URL}/${deviceId}/recent?limit=${limit}`);
}

export function getHarshBrakingCount(deviceId: string, sinceHours = 24): Promise<HarshEventCount> {
  return getJson(`${API_BASE_URL}/${deviceId}/harsh-braking-count?sinceHours=${sinceHours}`);
}

export function getHarshCorneringCount(deviceId: string, sinceHours = 24): Promise<HarshEventCount> {
  return getJson(`${API_BASE_URL}/${deviceId}/harsh-cornering-count?sinceHours=${sinceHours}`);
}

export function getHarshAccelerationCount(deviceId: string, sinceHours = 24): Promise<HarshEventCount> {
  return getJson(`${API_BASE_URL}/${deviceId}/harsh-acceleration-count?sinceHours=${sinceHours}`);
}
