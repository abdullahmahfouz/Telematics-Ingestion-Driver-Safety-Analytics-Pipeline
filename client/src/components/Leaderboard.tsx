import { Trophy } from "@phosphor-icons/react";
import type { LeaderboardEntry } from "../types/telematics";

interface LeaderboardProps {
  entries: LeaderboardEntry[];
  available: boolean;
  ref?: React.Ref<HTMLElement>;
}

export function Leaderboard({ entries, available, ref }: LeaderboardProps) {
  return (
    <section className="leaderboard" ref={ref}>
      <div className="leaderboard__header">
        <h2>
          <Trophy size={20} weight="bold" />
          Safety leaderboard
        </h2>
        <span className="leaderboard__scope">All devices</span>
      </div>

      {!available && (
        <p className="empty-state">Redis is unavailable right now, so the live leaderboard can't be shown.</p>
      )}

      {available && entries.length === 0 && <p className="empty-state">No harsh events recorded yet.</p>}

      {available && entries.length > 0 && (
        <ol className="leaderboard__list">
          {entries.map((entry) => (
            <li
              key={entry.deviceId}
              className={`leaderboard__row${entry.rank <= 3 ? ` leaderboard__row--top${entry.rank}` : ""}`}
            >
              <span className="leaderboard__rank">{entry.rank}</span>
              <span className="leaderboard__device">{entry.deviceId}</span>
              <span className="leaderboard__count">{entry.harshEventCount}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
