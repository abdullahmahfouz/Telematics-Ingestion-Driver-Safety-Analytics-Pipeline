import { useCallback, useEffect, useRef, useState } from "react";
import "./App.css";
import {
  ChatCircleDots,
  Gauge,
  MagnifyingGlass,
  MapTrifold,
  SignOut,
  SteeringWheel,
  Trophy,
  WarningCircle,
  type Icon,
} from "@phosphor-icons/react";
import { StatCard } from "./components/StatCard";
import { RecentRecordsTable } from "./components/RecentRecordsTable";
import { TripMap } from "./components/TripMap";
import { AssistantPanel } from "./components/AssistantPanel";
import { ErrorBanner } from "./components/ErrorBanner";
import { LoginScreen } from "./components/LoginScreen";
import { Leaderboard } from "./components/Leaderboard";
import {
  getHarshAccelerationCount,
  getHarshBrakingCount,
  getHarshCorneringCount,
  getLeaderboard,
  getRecentRecords,
} from "./api/telematicsApi";
import { clearToken, getToken, SessionExpiredError } from "./api/authToken";
import type { LeaderboardEntry, TelematicsRecord } from "./types/telematics";

function App() {
  const [deviceId, setDeviceId] = useState("b2A83F1");
  const [deviceIdInput, setDeviceIdInput] = useState("b2A83F1");
  const [records, setRecords] = useState<TelematicsRecord[]>([]);
  const [brakingCount, setBrakingCount] = useState<number | null>(null);
  const [corneringCount, setCorneringCount] = useState<number | null>(null);
  const [accelerationCount, setAccelerationCount] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [authed, setAuthed] = useState(() => !!getToken());
  const [leaderboardEntries, setLeaderboardEntries] = useState<LeaderboardEntry[]>([]);
  const [leaderboardAvailable, setLeaderboardAvailable] = useState(true);

  const handleSessionExpired = useCallback(() => {
    clearToken();
    setAuthed(false);
  }, []);

  const overviewRef = useRef<HTMLElement>(null);
  const leaderboardRef = useRef<HTMLElement>(null);
  const mapRef = useRef<HTMLElement>(null);
  const assistantRef = useRef<HTMLElement>(null);

  const sections: { ref: React.RefObject<HTMLElement | null>; icon: Icon; label: string }[] = [
    { ref: overviewRef, icon: Gauge, label: "Overview" },
    { ref: leaderboardRef, icon: Trophy, label: "Safety leaderboard" },
    { ref: mapRef, icon: MapTrifold, label: "Map & trips" },
    { ref: assistantRef, icon: ChatCircleDots, label: "Safety assistant" },
  ];

  const refresh = useCallback(async () => {
    setError(null);
    try {
      const [recent, braking, cornering, acceleration] = await Promise.all([
        getRecentRecords(deviceId, 20),
        getHarshBrakingCount(deviceId, 24),
        getHarshCorneringCount(deviceId, 24),
        getHarshAccelerationCount(deviceId, 24),
      ]);
      setRecords(recent);
      setBrakingCount(braking.harshBrakingEventCount ?? 0);
      setCorneringCount(cornering.harshCorneringEventCount ?? 0);
      setAccelerationCount(acceleration.harshAccelerationEventCount ?? 0);
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        handleSessionExpired();
        return;
      }
      setError("Could not reach the API. Is it running at http://localhost:5231?");
    }
  }, [deviceId, handleSessionExpired]);

  useEffect(() => {
    if (!authed) return;
    refresh();
    const intervalId = setInterval(refresh, 5000);
    return () => clearInterval(intervalId);
  }, [refresh, authed]);

  const refreshLeaderboard = useCallback(async () => {
    try {
      const { available, entries } = await getLeaderboard(10);
      setLeaderboardAvailable(available);
      setLeaderboardEntries(entries);
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        handleSessionExpired();
      }
      // A leaderboard fetch failure otherwise isn't surfaced as a page-level error --
      // it's a secondary panel, and the main refresh() already reports connectivity issues.
    }
  }, [handleSessionExpired]);

  // Fleet-wide, not scoped to the selected device, so this doesn't depend on deviceId
  // and shouldn't re-fetch every time someone looks up a different device.
  useEffect(() => {
    if (!authed) return;
    refreshLeaderboard();
    const intervalId = setInterval(refreshLeaderboard, 5000);
    return () => clearInterval(intervalId);
  }, [refreshLeaderboard, authed]);

  function scrollTo(ref: React.RefObject<HTMLElement | null>) {
    ref.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  if (!authed) {
    return <LoginScreen onLoggedIn={() => setAuthed(true)} />;
  }

  return (
    <div className="app-shell">
      <nav className="sidebar" aria-label="Sections">
        <div className="sidebar__mark" aria-hidden="true">
          <SteeringWheel size={20} weight="bold" />
        </div>
        {sections.map(({ ref, icon: SectionIcon, label }) => (
          <button
            key={label}
            type="button"
            className="sidebar__item"
            title={label}
            onClick={() => scrollTo(ref)}
          >
            <SectionIcon size={20} />
          </button>
        ))}
        <button
          type="button"
          className="sidebar__item sidebar__item--logout"
          title="Log out"
          onClick={handleSessionExpired}
        >
          <SignOut size={20} />
        </button>
      </nav>

      <div className="dashboard">
        <header className="dashboard__header" ref={overviewRef}>
          <div className="dashboard__title">
            <h1>Driver safety dashboard</h1>
            <span className={`status-pill ${error ? "status-pill--offline" : "status-pill--live"}`}>
              <span className="status-pill__dot" />
              {error ? "Disconnected" : "Live"}
            </span>
          </div>
          <form
            className="device-lookup"
            onSubmit={(e) => {
              e.preventDefault();
              setDeviceId(deviceIdInput.trim());
            }}
          >
            <input
              value={deviceIdInput}
              onChange={(e) => setDeviceIdInput(e.target.value)}
              placeholder="Device ID"
              aria-label="Device ID"
            />
            <button type="submit">
              <MagnifyingGlass size={15} />
              Look up
            </button>
          </form>
        </header>

        {error && <ErrorBanner message={error} />}

        <p className="scope-note">
          Showing data for <code>{deviceId}</code> only — the readings, stats, map and table below all
          scope to this one device. Look up a different device above to switch. The leaderboard further
          down is the only section that spans every device.
        </p>

        <section className="stat-row">
          <StatCard label="Readings loaded" value={records.length} icon={Gauge} />
          <StatCard
            label="Harsh braking (24h)"
            value={brakingCount ?? "…"}
            tone={brakingCount ? "braking" : "neutral"}
            icon={WarningCircle}
          />
          <StatCard
            label="Harsh cornering (24h)"
            value={corneringCount ?? "…"}
            tone={corneringCount ? "cornering" : "neutral"}
            icon={WarningCircle}
          />
          <StatCard
            label="Harsh acceleration (24h)"
            value={accelerationCount ?? "…"}
            tone={accelerationCount ? "acceleration" : "neutral"}
            icon={WarningCircle}
          />
        </section>

        <Leaderboard entries={leaderboardEntries} available={leaderboardAvailable} ref={leaderboardRef} />

        <section className="dashboard__main" ref={mapRef}>
          <div className="panel-card">
            <TripMap records={records} />
          </div>
          <div className="panel-card panel-card--scroll">
            <RecentRecordsTable records={records} />
          </div>
        </section>

        <AssistantPanel deviceId={deviceId} onSessionExpired={handleSessionExpired} ref={assistantRef} />
      </div>
    </div>
  );
}

export default App;
