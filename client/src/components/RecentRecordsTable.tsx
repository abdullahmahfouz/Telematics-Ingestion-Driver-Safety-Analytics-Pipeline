import type { TelematicsRecord } from "../types/telematics";
import { isHarshAcceleration, isHarshBraking, isHarshCornering } from "../safety/thresholds";

interface RecentRecordsTableProps {
  records: TelematicsRecord[];
}

export function RecentRecordsTable({ records }: RecentRecordsTableProps) {
  if (records.length === 0) {
    return <p className="empty-state">No readings yet for this device.</p>;
  }

  return (
    <table className="records-table">
      <thead>
        <tr>
          <th>Time</th>
          <th>Speed (km/h)</th>
          <th>Accel X (g)</th>
          <th>Accel Y (g)</th>
          <th>Flags</th>
        </tr>
      </thead>
      <tbody>
        {records.map((record) => {
          const braking = isHarshBraking(record.accelerationXG);
          const cornering = isHarshCornering(record.accelerationYG);
          const acceleration = isHarshAcceleration(record.accelerationXG);

          return (
            <tr key={record.id}>
              <td>{new Date(record.timestamp).toLocaleTimeString()}</td>
              <td>{record.speedKmh.toFixed(1)}</td>
              <td>{record.accelerationXG?.toFixed(2) ?? "—"}</td>
              <td>{record.accelerationYG?.toFixed(2) ?? "—"}</td>
              <td>
                {braking && <span className="flag flag--braking">harsh braking</span>}
                {cornering && <span className="flag flag--cornering">harsh cornering</span>}
                {acceleration && <span className="flag flag--acceleration">harsh acceleration</span>}
                {!braking && !cornering && !acceleration && <span className="flag flag--ok">normal</span>}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
