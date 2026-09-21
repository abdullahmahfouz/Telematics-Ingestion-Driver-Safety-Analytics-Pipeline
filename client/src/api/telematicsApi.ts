import type { HarshEventCount, TelematicsRecord } from "../types/telematics";

const API_BASE_URL = "http://localhost:5231/api/telematics";

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Request to ${url} failed with status ${response.status}`);
  }
  return response.json() as Promise<T>;
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

export async function ingestRecord(record: Partial<TelematicsRecord>): Promise<TelematicsRecord> {
  const response = await fetch(`${API_BASE_URL}/ingest`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(record),
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(`Ingest failed with status ${response.status}: ${JSON.stringify(problem)}`);
  }
  return response.json() as Promise<TelematicsRecord>;
}
