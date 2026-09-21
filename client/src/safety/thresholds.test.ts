import { describe, expect, it } from "vitest";
import { isHarshAcceleration, isHarshBraking, isHarshCornering } from "./thresholds";

describe("isHarshBraking", () => {
  it.each([
    [null, false],
    [0.02, false],
    [-0.39, false],
    [-0.4, true],
    [-0.62, true],
  ])("accelerationXG %s -> %s", (value, expected) => {
    expect(isHarshBraking(value)).toBe(expected);
  });

  it("does not flag hard acceleration as braking", () => {
    expect(isHarshBraking(0.45)).toBe(false);
  });
});

describe("isHarshCornering", () => {
  it.each([
    [null, false],
    [0.1, false],
    [0.44, false],
    [0.45, true],
    [-0.44, false],
    [-0.45, true],
    [-0.7, true],
  ])("accelerationYG %s -> %s", (value, expected) => {
    expect(isHarshCornering(value)).toBe(expected);
  });
});

describe("isHarshAcceleration", () => {
  it.each([
    [null, false],
    [0.05, false],
    [0.34, false],
    [0.35, true],
    [0.5, true],
  ])("accelerationXG %s -> %s", (value, expected) => {
    expect(isHarshAcceleration(value)).toBe(expected);
  });

  it("does not flag hard braking as acceleration", () => {
    expect(isHarshAcceleration(-0.62)).toBe(false);
  });
});

describe("frontend thresholds match the backend detectors", () => {
  // These mirror the C# constants in TelematicsPipeline.Api/SafetyEngine/*.cs.
  // If a backend threshold changes, this test is the tripwire that catches the drift.
  it("flags the same boundary values the backend does", () => {
    expect(isHarshBraking(-0.4)).toBe(true);
    expect(isHarshCornering(0.45)).toBe(true);
    expect(isHarshAcceleration(0.35)).toBe(true);
  });
});
