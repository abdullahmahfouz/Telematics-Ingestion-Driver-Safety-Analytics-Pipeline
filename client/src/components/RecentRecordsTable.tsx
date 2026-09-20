import type { TelematicsRecord } from "../types/telematics";
import { HarshBrakingThresholdG, HarshCorneringThresholdG } from "../safety/thresholds";

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
          const isHarshBraking =
            record.accelerationXG !== null && record.accelerationXG <= HarshBrakingThresholdG;
          const isHarshCornering =
            record.accelerationYG !== null && Math.abs(record.accelerationYG) >= HarshCorneringThresholdG;

          return (
            <tr key={record.id}>
              <td>{new Date(record.timestamp).toLocaleTimeString()}</td>
              <td>{record.speedKmh.toFixed(1)}</td>
              <td>{record.accelerationXG?.toFixed(2) ?? "—"}</td>
              <td>{record.accelerationYG?.toFixed(2) ?? "—"}</td>
              <td>
                {isHarshBraking && <span className="flag flag--danger">harsh braking</span>}
                {isHarshCornering && <span className="flag flag--danger">harsh cornering</span>}
                {!isHarshBraking && !isHarshCornering && <span className="flag flag--ok">normal</span>}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
