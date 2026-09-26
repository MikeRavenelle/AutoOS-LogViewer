import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ChannelDto, GetRawSeriesRequest, GetRawSeriesResult, TimeRangeDto } from "./protocol";
import { DEFAULT_UNIT_PREFS, displayUnit, displayValue, type UnitPrefs } from "./units";

export interface ReplayViewProps {
  channels: ChannelDto[];
  fullRange: TimeRangeDto;
  fetchRawSeries: (req: Omit<GetRawSeriesRequest, "logId">) => Promise<GetRawSeriesResult>;
  unitPrefs?: UnitPrefs;
}

const MAX_GAUGES = 8;
const SPEEDS = [0.25, 0.5, 1, 2, 4, 8];

function defaultGauges(channels: ChannelDto[]): number[] {
  const find = (prefix: string) => channels.find((c) => c.name.startsWith(prefix))?.id;
  const preferred = [
    find("Boost Pressure Actual"),
    find("Boost Pressure Target"),
    find("Boost Error"),
    find("Engine Speed"),
  ].filter((id): id is number => id !== undefined);
  if (preferred.length > 0) return preferred;

  return channels
    .filter((c) => !c.name.startsWith("Log Mark"))
    .slice(0, 4)
    .map((c) => c.id);
}

interface Loaded {
  t: number[];
  byChannel: Map<number, Float32Array>;
  minMax: Map<number, { min: number; max: number }>;
}

export function ReplayView({
  channels,
  fullRange,
  fetchRawSeries,
  unitPrefs = DEFAULT_UNIT_PREFS,
}: ReplayViewProps) {
  const [selected, setSelected] = useState<number[]>(() => defaultGauges(channels));
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [currentTime, setCurrentTime] = useState(fullRange.start);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(1);

  const channelById = useMemo(() => new Map(channels.map((c) => [c.id, c])), [channels]);

  useEffect(() => {
    if (selected.length === 0) {
      setLoaded(null);
      return;
    }
    let stale = false;
    fetchRawSeries({ channelIds: selected, t0: fullRange.start, t1: fullRange.end })
      .then((res) => {
        if (stale) return;
        setError(null);
        const byChannel = new Map<number, Float32Array>();
        const minMax = new Map<number, { min: number; max: number }>();
        for (const s of res.series) {
          byChannel.set(s.channelId, Float32Array.from(s.v));
          let min = Infinity;
          let max = -Infinity;
          for (const v of s.v) {
            if (Number.isNaN(v)) continue;
            if (v < min) min = v;
            if (v > max) max = v;
          }
          minMax.set(s.channelId, min <= max ? { min, max } : { min: 0, max: 1 });
        }
        setLoaded({ t: res.t, byChannel, minMax });
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : String(err)));
    return () => {
      stale = true;
    };
  }, [selected, fullRange, fetchRawSeries]);

  const rafRef = useRef<number | null>(null);
  const lastRef = useRef<number | null>(null);
  useEffect(() => {
    if (!playing) {
      lastRef.current = null;
      return;
    }
    const tick = (now: number) => {
      if (lastRef.current != null) {
        const dt = ((now - lastRef.current) / 1000) * speed;
        setCurrentTime((t) => {
          const next = t + dt;
          if (next >= fullRange.end) {
            setPlaying(false);
            return fullRange.end;
          }
          return next;
        });
      }
      lastRef.current = now;
      rafRef.current = requestAnimationFrame(tick);
    };
    rafRef.current = requestAnimationFrame(tick);
    return () => {
      if (rafRef.current != null) cancelAnimationFrame(rafRef.current);
    };
  }, [playing, speed, fullRange.end]);

  const toggleChannel = useCallback((id: number) => {
    setSelected((prev) => {
      if (prev.includes(id)) return prev.filter((x) => x !== id);
      if (prev.length >= MAX_GAUGES) return prev;
      return [...prev, id];
    });
  }, []);

  const valueAt = (channelId: number): number | null => {
    if (!loaded) return null;
    const col = loaded.byChannel.get(channelId);
    if (!col || loaded.t.length === 0) return null;
    const idx = nearestIndex(loaded.t, currentTime);
    const v = col[idx];
    return Number.isNaN(v) ? null : v;
  };

  const togglePlay = () => {
    if (!playing && currentTime >= fullRange.end) setCurrentTime(fullRange.start);
    setPlaying((p) => !p);
  };

  return (
    <div className="replay-view">
      <div className="replay-controls">
        <button className="secondary" onClick={togglePlay} disabled={selected.length === 0}>
          {playing ? "Pause" : "Play"}
        </button>
        <label className="offset-label">
          speed
          <select value={speed} onChange={(e) => setSpeed(Number(e.target.value))}>
            {SPEEDS.map((s) => (
              <option key={s} value={s}>
                {s}×
              </option>
            ))}
          </select>
        </label>
        <input
          className="replay-scrub"
          type="range"
          min={fullRange.start}
          max={fullRange.end}
          step={(fullRange.end - fullRange.start) / 2000 || 0.01}
          value={currentTime}
          onChange={(e) => {
            setPlaying(false);
            setCurrentTime(Number(e.target.value));
          }}
        />
        <span className="replay-time">
          {currentTime.toFixed(2)}s / {fullRange.end.toFixed(2)}s
        </span>
        {error && <span className="math-error">{error}</span>}
      </div>
      <div className="replay-body">
        <ul className="channel-list replay-picker">
          {channels.map((c) => (
            <li key={c.id}>
              <button
                className={`channel${selected.includes(c.id) ? " selected" : ""}`}
                onClick={() => toggleChannel(c.id)}
              >
                {(c.computed ? "ƒ " : "") + c.name}
                {c.unit && <span className="unit"> ({displayUnit(c.unit, unitPrefs)})</span>}
              </button>
            </li>
          ))}
        </ul>
        <p className="sidebar-hint">Up to {MAX_GAUGES} gauges at once.</p>
        <div className="gauge-grid">
          {selected.map((id) => {
            const c = channelById.get(id);
            const v = valueAt(id);
            const range = loaded?.minMax.get(id) ?? { min: 0, max: 1 };
            const frac = v == null ? 0 : clamp01((v - range.min) / (range.max - range.min || 1));
            const baseUnit = c?.unit ?? "";
            const dv = v == null ? null : displayValue(v, baseUnit, unitPrefs);
            const dMin = displayValue(range.min, baseUnit, unitPrefs).value;
            const dMax = displayValue(range.max, baseUnit, unitPrefs).value;
            return (
              <div className="gauge" key={id}>
                <div className="gauge-label">{c?.name ?? `#${id}`}</div>
                <div className="gauge-value">
                  {dv == null ? "--" : dv.value.toFixed(2)}
                  {dv?.unit && <span className="gauge-unit"> {dv.unit}</span>}
                </div>
                <div className="gauge-bar">
                  <div className="gauge-bar-fill" style={{ width: `${frac * 100}%` }} />
                </div>
                <div className="gauge-minmax">
                  <span>{dMin.toFixed(1)}</span>
                  <span>{dMax.toFixed(1)}</span>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

function clamp01(x: number): number {
  return Math.max(0, Math.min(1, x));
}

function nearestIndex(xs: number[], t: number): number {
  let lo = 0;
  let hi = xs.length - 1;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    if (xs[mid] < t) lo = mid + 1;
    else hi = mid;
  }
  if (lo > 0 && Math.abs(xs[lo - 1] - t) < Math.abs(xs[lo] - t)) return lo - 1;
  return lo;
}
