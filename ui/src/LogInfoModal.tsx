import type { OpenLogResult } from "./protocol";

export interface LogInfoModalProps {
  log: OpenLogResult;
  parserName: string;
  onClose: () => void;
}

export function LogInfoModal({ log, parserName, onClose }: LogInfoModalProps) {
  const duration = (log.timeRange.end - log.timeRange.start).toFixed(1);
  const metadataEntries = Object.entries(log.metadata);

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <span>Log info</span>
          <button className="chip-x" title="Close" onClick={onClose}>
            ×
          </button>
        </div>
        <table className="modal-table">
          <tbody>
            <tr>
              <th>File</th>
              <td>{log.sourcePath}</td>
            </tr>
            <tr>
              <th>Parser</th>
              <td>{parserName}</td>
            </tr>
            <tr>
              <th>Samples</th>
              <td>{log.sampleCount.toLocaleString()}</td>
            </tr>
            <tr>
              <th>Duration</th>
              <td>{duration} s</td>
            </tr>
            <tr>
              <th>Channels</th>
              <td>{log.channels.length}</td>
            </tr>
            {metadataEntries.map(([key, value]) => (
              <tr key={key}>
                <th>{key}</th>
                <td>{value}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
