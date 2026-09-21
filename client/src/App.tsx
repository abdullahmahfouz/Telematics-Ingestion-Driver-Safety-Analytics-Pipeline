import { useCallback, useEffect, useState } from "react";
import "./App.css";
import { Gauge, MagnifyingGlass, SignOut, SteeringWheel, WarningCircle } from "@phosphor-icons/react";
import { StatCard } from "./components/StatCard";
import { RecentRecordsTable } from "./components/RecentRecordsTable";
import { TripMap } from "./components/TripMap";
import { AssistantPanel } from "./components/AssistantPanel";
import { ErrorBanner } from "./components/ErrorBanner";
import { LoginScreen } from "./components/LoginScreen";
import {
  getDevices,
  getHarshAccelerationCount,
  getHarshBrakingCount,
  getHarshCorneringCount,
  getRecentRecords,
} from "./api/telematicsApi";
import { clearToken, getToken, SessionExpiredError } from "./api/authToken";
import type { TelematicsRecord } from "./types/telematics";

function App() {
  const [deviceId, setDeviceId] = useState("b2A83F1");
  const [deviceIdInput, setDeviceIdInput] = useState("b2A83F1");
  const [knownDeviceIds, setKnownDeviceIds] = useState<string[]>([]);
  const [records, setRecords] = useState<TelematicsRecord[]>([]);
  const [brakingCount, setBrakingCount] = useState<number | null>(null);
  const [corneringCount, setCorneringCount] = useState<number | null>(null);
  const [accelerationCount, setAccelerationCount] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [authed, setAuthed] = useState(() => !!getToken());

  const handleSessionExpired = useCallback(() => {
    clearToken();
    setAuthed(false);
  }, []);

  function lookUpDevice(nextDeviceId: string) {
    setDeviceIdInput(nextDeviceId);
    setDeviceId(nextDeviceId);
  }

  // Fleet-wide, not scoped to the selected device -- lets someone testing the dashboard see
  // what device IDs actually have data instead of having to already know or guess one.
  useEffect(() => {
    if (!authed) return;
    getDevices()
      .then(setKnownDeviceIds)
      .catch((err) => {
        if (err instanceof SessionExpiredError) handleSessionExpired();
      });
  }, [authed, handleSessionExpired]);

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

  if (!authed) {
    return <LoginScreen onLoggedIn={() => setAuthed(true)} />;
  }

  return (
    <div className="app-shell">
      <nav className="sidebar" aria-label="Navigation">
        <div className="sidebar__mark" aria-hidden="true">
          <SteeringWheel size={20} weight="bold" />
        </div>
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
        <header className="dashboard__header">
          <div className="dashboard__title">
            <h1>Driver safety dashboard</h1>
            {error && (
              <span className="status-pill status-pill--offline">
                <span className="status-pill__dot" />
                Disconnected
              </span>
            )}
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
          scope to this one device.
        </p>

        {knownDeviceIds.length > 0 && (
          <div className="device-chips">
            <span className="device-chips__label">Known devices, for testing:</span>
            {knownDeviceIds.map((id) => (
              <button
                key={id}
                type="button"
                className={`device-chip${id === deviceId ? " device-chip--active" : ""}`}
                onClick={() => lookUpDevice(id)}
              >
                {id}
              </button>
            ))}
          </div>
        )}

        <div className="dashboard__body">
          <div className="dashboard__content">
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

            <section className="dashboard__main">
              <div className="panel-card">
                <TripMap records={records} />
              </div>
              <div className="panel-card panel-card--scroll">
                <RecentRecordsTable records={records} />
              </div>
            </section>
          </div>

          <AssistantPanel deviceId={deviceId} onSessionExpired={handleSessionExpired} />
        </div>
      </div>
    </div>
  );
}

export default App;
