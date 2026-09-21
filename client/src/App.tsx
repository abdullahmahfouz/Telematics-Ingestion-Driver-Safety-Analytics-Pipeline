import { useCallback, useEffect, useRef, useState } from "react";
import "./App.css";
import {
  ArrowsClockwise,
  ChatCircleDots,
  Gauge,
  MagnifyingGlass,
  MapTrifold,
  SignOut,
  SteeringWheel,
  WarningCircle,
  type Icon,
} from "@phosphor-icons/react";
import { StatCard } from "./components/StatCard";
import { RecentRecordsTable } from "./components/RecentRecordsTable";
import { TripMap } from "./components/TripMap";
import { AssistantPanel } from "./components/AssistantPanel";
import { ErrorBanner } from "./components/ErrorBanner";
import { LoginScreen } from "./components/LoginScreen";
import {
  getHarshAccelerationCount,
  getHarshBrakingCount,
  getHarshCorneringCount,
  getRecentRecords,
  ingestRecord,
} from "./api/telematicsApi";
import { clearToken, getToken, SessionExpiredError } from "./api/authToken";
import type { TelematicsRecord } from "./types/telematics";

const BASE_LATITUDE = 43.685;
const BASE_LONGITUDE = -79.345;

function jitter(base: number, magnitude: number): number {
  return base + (Math.random() - 0.5) * magnitude;
}

function App() {
  const [deviceId, setDeviceId] = useState("b2A83F1");
  const [deviceIdInput, setDeviceIdInput] = useState("b2A83F1");
  const [records, setRecords] = useState<TelematicsRecord[]>([]);
  const [brakingCount, setBrakingCount] = useState<number | null>(null);
  const [corneringCount, setCorneringCount] = useState<number | null>(null);
  const [accelerationCount, setAccelerationCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [authed, setAuthed] = useState(() => !!getToken());

  const handleSessionExpired = useCallback(() => {
    clearToken();
    setAuthed(false);
  }, []);

  const overviewRef = useRef<HTMLElement>(null);
  const mapRef = useRef<HTMLElement>(null);
  const assistantRef = useRef<HTMLElement>(null);

  const sections: { ref: React.RefObject<HTMLElement | null>; icon: Icon; label: string }[] = [
    { ref: overviewRef, icon: Gauge, label: "Overview" },
    { ref: mapRef, icon: MapTrifold, label: "Map & trips" },
    { ref: assistantRef, icon: ChatCircleDots, label: "Safety assistant" },
  ];

  const refresh = useCallback(async () => {
    setLoading(true);
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
    } finally {
      setLoading(false);
    }
  }, [deviceId, handleSessionExpired]);

  useEffect(() => {
    if (!authed) return;
    refresh();
    const intervalId = setInterval(refresh, 5000);
    return () => clearInterval(intervalId);
  }, [refresh, authed]);

  async function sendTestReading(kind: "normal" | "harsh-braking" | "harsh-cornering" | "harsh-acceleration") {
    setSending(true);
    setError(null);
    try {
      await ingestRecord({
        deviceId,
        timestamp: new Date().toISOString(),
        latitude: jitter(BASE_LATITUDE, 0.01),
        longitude: jitter(BASE_LONGITUDE, 0.01),
        speedKmh: kind === "harsh-braking" ? 55 : 70,
        headingDegrees: 180,
        odometerKm: 84300,
        isIdling: false,
        isIgnitionOn: true,
        accelerationXG:
          kind === "harsh-braking" ? -0.65 : kind === "harsh-acceleration" ? 0.45 : 0.05,
        accelerationYG: kind === "harsh-cornering" ? (Math.random() > 0.5 ? 0.6 : -0.6) : 0.02,
      });
      await refresh();
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        handleSessionExpired();
        return;
      }
      setError("Failed to send the test reading.");
    } finally {
      setSending(false);
    }
  }

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

        <section className="stat-row">
          <StatCard label="Readings loaded" value={records.length} icon={Gauge} />
          <StatCard
            label="Harsh braking (24h)"
            value={brakingCount ?? "…"}
            tone={brakingCount ? "warning" : "neutral"}
            icon={WarningCircle}
          />
          <StatCard
            label="Harsh cornering (24h)"
            value={corneringCount ?? "…"}
            tone={corneringCount ? "warning" : "neutral"}
            icon={WarningCircle}
          />
          <StatCard
            label="Harsh acceleration (24h)"
            value={accelerationCount ?? "…"}
            tone={accelerationCount ? "warning" : "neutral"}
            icon={WarningCircle}
          />
        </section>

        <section className="test-actions">
          <span>Send a test reading:</span>
          <button disabled={sending} onClick={() => sendTestReading("normal")}>
            Normal
          </button>
          <button disabled={sending} onClick={() => sendTestReading("harsh-braking")}>
            Harsh braking
          </button>
          <button disabled={sending} onClick={() => sendTestReading("harsh-cornering")}>
            Harsh cornering
          </button>
          <button disabled={sending} onClick={() => sendTestReading("harsh-acceleration")}>
            Harsh acceleration
          </button>
          <button disabled={loading} onClick={() => refresh()}>
            <ArrowsClockwise size={14} className={loading ? "spin" : undefined} />
            Refresh
          </button>
        </section>

        <section className="dashboard__main" ref={mapRef}>
          <TripMap records={records} />
          <RecentRecordsTable records={records} />
        </section>

        <AssistantPanel deviceId={deviceId} onSessionExpired={handleSessionExpired} ref={assistantRef} />
      </div>
    </div>
  );
}

export default App;
