import type { ChannelDto, ChannelStatsDto, TimeRangeDto } from "./protocol";
import { DEFAULT_UNIT_PREFS, displayDelta, displayLabel, displayValue, type UnitPrefs } from "./units";

export interface SelectionStatsPanelProps {
  range: TimeRangeDto;
  stats: ChannelStatsDto[];
  channels: ChannelDto[];
  onClose: () => void;
  unitPrefs?: UnitPrefs;
}

export function SelectionStatsPanel({
  range,
  stats,
  channels,
  onClose,
  unitPrefs = DEFAULT_UNIT_PREFS,
}: SelectionStatsPanelProps) {
  const byId = new Map(channels.map((c) => [c.id, c]));

  return (
    <div className="selection-stats">
      <div className="selection-stats-header">
        <span>
          Selection: {range.start.toFixed(3)}s – {range.end.toFixed(3)}s ({(range.end - range.start).toFixed(3)}s)
        </span>
        <button className="chip-x" title="Close" onClick={onClose}>
          ×
        </button>
      </div>
      <table className="selection-stats-table">
        <thead>
          <tr>
            <th>Channel</th>
            <th>Min</th>
            <th>Max</th>
            <th>Avg</th>
            <th title="Last sample in the selection minus the first">Δ</th>
            <th title="Population standard deviation (hover for variance)">σ</th>
            <th title="50th percentile (linear-interpolated)">Median</th>
            <th title="95th percentile (linear-interpolated)">P95</th>
            <th>N</th>
          </tr>
        </thead>
        <tbody>
          {stats.map((s) => {
            const c = byId.get(s.channelId);
            const unit = c?.unit ?? "";
            return (
              <tr key={s.channelId}>
                <td>{c ? displayLabel(c.name, unit, unitPrefs) : `#${s.channelId}`}</td>
                <td>{fmt(s.min, unit, unitPrefs)}</td>
                <td>{fmt(s.max, unit, unitPrefs)}</td>
                <td>{fmt(s.avg, unit, unitPrefs)}</td>
                <td className={deltaClass(s.delta)}>{fmtDelta(s.delta, unit, unitPrefs)}</td>
                <td title={s.variance === null ? undefined : `variance: ${s.variance.toFixed(3)}`}>
                  {fmt(s.stdDev, unit, unitPrefs)}
                </td>
                <td>{fmt(s.median, unit, unitPrefs)}</td>
                <td>{fmt(s.p95, unit, unitPrefs)}</td>
                <td>{s.count}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function fmt(v: number | null, unit: string, prefs: UnitPrefs): string {
  return v === null ? "--" : displayValue(v, unit, prefs).value.toFixed(2);
}

function fmtDelta(v: number | null, unit: string, prefs: UnitPrefs): string {
  if (v === null) return "--";
  const converted = displayDelta(v, unit, prefs).value;
  const sign = converted > 0 ? "+" : "";
  return `${sign}${converted.toFixed(2)}`;
}

function deltaClass(v: number | null): string {
  if (v === null || v === 0) return "";
  return v > 0 ? "selection-stats-delta-up" : "selection-stats-delta-down";
}
