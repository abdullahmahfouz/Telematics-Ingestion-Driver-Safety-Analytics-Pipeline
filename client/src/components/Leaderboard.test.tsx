import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Leaderboard } from "./Leaderboard";
import type { LeaderboardEntry } from "../types/telematics";

const entries: LeaderboardEntry[] = [
  { rank: 1, deviceId: "FLEET-103", harshEventCount: 9 },
  { rank: 2, deviceId: "FLEET-102", harshEventCount: 6 },
  { rank: 3, deviceId: "FLEET-101", harshEventCount: 4 },
];

describe("Leaderboard", () => {
  it("renders a ranked row per entry", () => {
    render(<Leaderboard entries={entries} available={true} />);

    expect(screen.getByText("FLEET-103")).toBeInTheDocument();
    expect(screen.getByText("9")).toBeInTheDocument();
    expect(screen.getByText("FLEET-102")).toBeInTheDocument();
    expect(screen.getByText("FLEET-101")).toBeInTheDocument();
  });

  it("shows an empty state when there are no entries yet", () => {
    render(<Leaderboard entries={[]} available={true} />);

    expect(screen.getByText(/no harsh events recorded yet/i)).toBeInTheDocument();
  });

  it("explains when Redis is unavailable instead of showing an empty board", () => {
    render(<Leaderboard entries={[]} available={false} />);

    expect(screen.getByText(/redis is unavailable/i)).toBeInTheDocument();
    expect(screen.queryByText(/no harsh events recorded yet/i)).not.toBeInTheDocument();
  });

  it("does not render entries if available is false, even if some were passed", () => {
    // Defensive: available=false should win even with stale entries from a previous fetch.
    render(<Leaderboard entries={entries} available={false} />);

    expect(screen.queryByText("FLEET-103")).not.toBeInTheDocument();
  });
});
