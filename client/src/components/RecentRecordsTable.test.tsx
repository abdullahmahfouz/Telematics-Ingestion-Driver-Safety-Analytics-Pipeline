import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RecentRecordsTable } from "./RecentRecordsTable";
import type { TelematicsRecord } from "../types/telematics";

function makeRecord(overrides: Partial<TelematicsRecord> = {}): TelematicsRecord {
  return {
    id: 1,
    deviceId: "b2A83F1",
    timestamp: "2026-09-20T13:42:00+00:00",
    latitude: 43.68,
    longitude: -79.34,
    speedKmh: 70,
    headingDegrees: 180,
    engineRpm: null,
    engineCoolantTempC: null,
    fuelLevelPercent: null,
    odometerKm: 84000,
    isIdling: false,
    isIgnitionOn: true,
    accelerationXG: 0.02,
    accelerationYG: 0.02,
    accelerationZG: null,
    ...overrides,
  };
}

describe("RecentRecordsTable", () => {
  it("shows an empty state when there are no records", () => {
    render(<RecentRecordsTable records={[]} />);

    expect(screen.getByText(/no readings yet/i)).toBeInTheDocument();
  });

  it("renders one row per record with speed formatted to one decimal", () => {
    render(<RecentRecordsTable records={[makeRecord({ id: 1, speedKmh: 66.25 })]} />);

    expect(screen.getByText("66.3")).toBeInTheDocument();
  });

  it("flags a harsh braking record", () => {
    render(<RecentRecordsTable records={[makeRecord({ accelerationXG: -0.62 })]} />);

    expect(screen.getByText("harsh braking")).toBeInTheDocument();
    expect(screen.queryByText("normal")).not.toBeInTheDocument();
  });

  it("flags a harsh cornering record in either direction", () => {
    render(
      <RecentRecordsTable
        records={[
          makeRecord({ id: 1, accelerationYG: 0.6 }),
          makeRecord({ id: 2, accelerationYG: -0.6 }),
        ]}
      />,
    );

    expect(screen.getAllByText("harsh cornering")).toHaveLength(2);
  });

  it("flags a harsh acceleration record", () => {
    render(<RecentRecordsTable records={[makeRecord({ accelerationXG: 0.45 })]} />);

    expect(screen.getByText("harsh acceleration")).toBeInTheDocument();
  });

  it("marks an unremarkable record as normal", () => {
    render(<RecentRecordsTable records={[makeRecord()]} />);

    expect(screen.getByText("normal")).toBeInTheDocument();
  });

  it("renders an em dash when acceleration data is missing", () => {
    render(<RecentRecordsTable records={[makeRecord({ accelerationXG: null, accelerationYG: null })]} />);

    expect(screen.getAllByText("—")).toHaveLength(2);
  });
});
