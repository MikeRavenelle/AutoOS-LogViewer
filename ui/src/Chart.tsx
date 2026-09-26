import { useEffect, useRef, useState } from "react";
import uPlot from "uplot";
import "uplot/dist/uPlot.min.css";
import type { TimeRangeDto as TimeRange } from "./protocol";
import { CHART_THEME } from "./theme";
import { DEFAULT_UNIT_PREFS, displayUnit, displayValue, type UnitPrefs } from "./units";

export interface SeriesSpec {
  channelId: number;
  label: string;
  unit: string;
  color: string;
  dashed?: boolean;
  thresholds?: { op: "gt" | "lt"; value: number }[];
}

const ALARM_COLOR = "#ff2d55";

export interface ChartProps {
  series: SeriesSpec[];
  data: uPlot.AlignedData | null;
  range: TimeRange;
  fullRange: TimeRange;
  onRangeChange: (range: TimeRange) => void;
  onWidthChange: (px: number) => void;
  onSelectionStats?: (range: TimeRange) => void;
  marks?: number[];
  annotations?: { time: number; text: string }[];
  onAddAnnotation?: (time: number) => void;
  yLocked?: boolean;
  unitPrefs?: UnitPrefs;
  resolvedDark?: boolean;
}

const MARK_COLOR = "#d3a34b";
const ANNOTATION_COLOR = "#2dd4bf";

const MIN_SPAN_S = 0.05;
const SYNC_KEY = "olv-cursor";

let dragIsStats = false;
let dragIsNote = false;

export function Chart({
  series,
  data,
  range,
  fullRange,
  onRangeChange,
  onWidthChange,
  onSelectionStats,
  marks,
  annotations,
  onAddAnnotation,
  yLocked,
  unitPrefs = DEFAULT_UNIT_PREFS,
  resolvedDark = true,
}: ChartProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  const plotRef = useRef<uPlot | null>(null);
  const [lockedRanges, setLockedRanges] = useState<Record<string, [number, number]> | null>(null);

  const stateRef = useRef({
    range,
    fullRange,
    onRangeChange,
    onSelectionStats,
    series,
    marks,
    annotations,
    onAddAnnotation,
    unitPrefs,
  });
  stateRef.current = {
    range,
    fullRange,
    onRangeChange,
    onSelectionStats,
    series,
    marks,
    annotations,
    onAddAnnotation,
    unitPrefs,
  };

  const seriesKey = series
    .map((s) => `${s.channelId}:${s.label}:${s.color}:${s.dashed ? 1 : 0}`)
    .join("|");
  const thresholdKey = series
    .map((s) => `${s.channelId}:${(s.thresholds ?? []).map((t) => `${t.op}${t.value}`).join(",")}`)
    .join("|");
  const marksKey = (marks ?? []).join(",");
  const annotationsKey = (annotations ?? []).map((a) => `${a.time}:${a.text}`).join("|");
  const unitPrefsKey = `${unitPrefs.pressure}:${unitPrefs.temp}`;
  const lockKey = lockedRanges ? Object.entries(lockedRanges).map(([k, r]) => `${k}:${r[0]}:${r[1]}`).join("|") : "";

  useEffect(() => {
    const plot = plotRef.current;
    if (!plot) return;
    if (yLocked && !lockedRanges) {
      const ranges: Record<string, [number, number]> = {};
      for (const key of Object.keys(plot.scales)) {
        if (key === "x") continue;
        const sc = plot.scales[key];
        if (sc.min != null && sc.max != null) ranges[key] = [sc.min, sc.max];
      }
      setLockedRanges(ranges);
    } else if (!yLocked && lockedRanges) {
      setLockedRanges(null);
    }
  }, [yLocked, lockedRanges]);

  useEffect(() => {
    const host = hostRef.current;
    if (!host || series.length === 0) return;

    const t = resolvedDark ? CHART_THEME.dark : CHART_THEME.light;
    const axisStyle = {
      stroke: t.axisStroke,
      grid: { stroke: t.gridStroke, width: 1 },
      ticks: { stroke: t.gridStroke, width: 1 },
    };

    const units: string[] = [];
    for (const s of series) {
      const u = s.unit || "·";
      if (!units.includes(u)) units.push(u);
    }
    const scaleFor = (s: SeriesSpec) => `y-${s.unit || "·"}`;

    const mousedown = (e: MouseEvent) => {
      dragIsStats = e.shiftKey;
      dragIsNote = e.ctrlKey || e.metaKey;
    };

    const axes: uPlot.Axis[] = [axisStyle];
    units.slice(0, 2).forEach((u, i) => {
      axes.push({
        ...axisStyle,
        scale: `y-${u}`,
        side: i === 0 ? 3 : 1,
        label: u === "·" ? undefined : () => displayUnit(u, stateRef.current.unitPrefs),
        labelSize: 14,
        values: (_u2, splits) =>
          splits.map((v) => (v == null ? "" : formatTick(displayValue(v, u, stateRef.current.unitPrefs).value))),
      });
    });

    const yScales: Record<string, uPlot.Scale> = {};
    for (const u of units) {
      const key = `y-${u}`;
      const locked = lockedRanges?.[key];
      yScales[key] = locked ? { auto: false, range: locked } : { auto: true };
    }

    const plot = new uPlot(
      {
        width: host.clientWidth || 800,
        height: host.clientHeight || 300,
        padding: [8, 8, 0, 0],
        scales: { x: { time: false, auto: false }, ...yScales },
        series: [
          { label: "Time (s)", value: (_u, v) => (v == null ? "--" : v.toFixed(3)) },
          ...series.map(
            (s): uPlot.Series => ({
              label: s.label,
              stroke: s.color,
              width: 1.25,
              dash: s.dashed ? [6, 4] : undefined,
              scale: scaleFor(s),
              spanGaps: true,
              value: (_u, v) =>
                v == null ? "--" : displayValue(v, s.unit, stateRef.current.unitPrefs).value.toFixed(3),
            }),
          ),
        ],
        axes,
        cursor: {
          drag: { x: true, y: false, setScale: false },
          sync: { key: SYNC_KEY, setSeries: false },
        },
        legend: { live: true },
        hooks: {
          setSelect: [
            (u) => {
              if (u.select.width < 5) {
                if (dragIsNote) {
                  const t = u.posToVal(u.select.left, "x");
                  u.setSelect({ left: 0, top: 0, width: 0, height: 0 }, false);
                  stateRef.current.onAddAnnotation?.(t);
                }
                return;
              }
              const t0 = u.posToVal(u.select.left, "x");
              const t1 = u.posToVal(u.select.left + u.select.width, "x");
              u.setSelect({ left: 0, top: 0, width: 0, height: 0 }, false);
              const selection = clamp({ start: t0, end: t1 }, stateRef.current.fullRange);
              if (dragIsStats) stateRef.current.onSelectionStats?.(selection);
              else stateRef.current.onRangeChange(selection);
            },
          ],
          draw: [drawAlarmMarkers(stateRef), drawLogMarks(stateRef), drawAnnotations(stateRef)],
        },
      },
      data && data.length === series.length + 1
        ? data
        : ([[], ...series.map(() => [])] as uPlot.AlignedData),
      host,
    );
    plotRef.current = plot;

    const legendHeight = () =>
      (plot.root.querySelector(".u-legend") as HTMLElement | null)?.offsetHeight ?? 0;
    const fit = () =>
      plot.setSize({
        width: host.clientWidth,
        height: Math.max(120, host.clientHeight - legendHeight()),
      });
    fit();
    plot.setScale("x", { min: range.start, max: range.end });

    const wheel = (e: WheelEvent) => {
      e.preventDefault();
      const { range, fullRange, onRangeChange } = stateRef.current;
      const span = range.end - range.start;

      const panDelta = e.shiftKey ? e.deltaY : e.deltaX;
      if (Math.abs(panDelta) > Math.abs(e.deltaY) || e.shiftKey) {
        const shift = (panDelta / plot.over.clientWidth) * span;
        onRangeChange(clamp({ start: range.start + shift, end: range.end + shift }, fullRange, true));
        return;
      }

      const rect = plot.over.getBoundingClientRect();
      const tCursor = plot.posToVal(e.clientX - rect.left, "x");
      const factor = e.deltaY < 0 ? 0.8 : 1.25;
      const newSpan = Math.max(span * factor, MIN_SPAN_S);
      const ratio = (tCursor - range.start) / span;
      onRangeChange(
        clamp({ start: tCursor - newSpan * ratio, end: tCursor + newSpan * (1 - ratio) }, fullRange),
      );
    };
    const dblclick = () => stateRef.current.onRangeChange(stateRef.current.fullRange);

    host.tabIndex = 0;
    const focusHost = () => host.focus({ preventScroll: true });
    const keydown = (e: KeyboardEvent) => {
      if (e.key !== "ArrowLeft" && e.key !== "ArrowRight") return;
      e.preventDefault();
      const xs = plot.data[0];
      if (xs.length === 0) return;
      const dir = e.key === "ArrowLeft" ? -1 : 1;
      const step = e.shiftKey ? 10 : 1;

      const { range, fullRange, onRangeChange } = stateRef.current;
      let i: number;
      if (plot.cursor.idx == null) {
        i = nearestIndex(xs, (range.start + range.end) / 2);
      } else {
        i = Math.min(xs.length - 1, Math.max(0, plot.cursor.idx + dir * step));
      }
      const t = xs[i];

      if (t < range.start || t > range.end) {
        const shift = t < range.start ? t - range.start : t - range.end;
        onRangeChange(clamp({ start: range.start + shift, end: range.end + shift }, fullRange, true));
      }
      plot.setCursor({ left: plot.valToPos(t, "x"), top: plot.cursor.top ?? 0 });
    };
    host.addEventListener("mouseenter", focusHost);
    host.addEventListener("keydown", keydown);

    plot.over.addEventListener("wheel", wheel, { passive: false });
    plot.over.addEventListener("dblclick", dblclick);
    plot.over.addEventListener("mousedown", mousedown);

    const ro = new ResizeObserver(() => {
      fit();
      onWidthChange(host.clientWidth);
    });
    ro.observe(host);
    onWidthChange(host.clientWidth || 800);

    return () => {
      ro.disconnect();
      host.removeEventListener("mouseenter", focusHost);
      host.removeEventListener("keydown", keydown);
      plot.over.removeEventListener("wheel", wheel);
      plot.over.removeEventListener("dblclick", dblclick);
      plot.over.removeEventListener("mousedown", mousedown);
      plot.destroy();
      plotRef.current = null;
    };
  }, [seriesKey, lockKey, resolvedDark]);

  useEffect(() => {
    const plot = plotRef.current;
    if (!plot) return;
    plot.setData(data ?? ([[], ...series.map(() => [])] as uPlot.AlignedData), false);
    plot.setScale("x", { min: range.start, max: range.end });
  }, [data, range]);

  useEffect(() => {
    const plot = plotRef.current;
    if (!plot) return;
    plot.setScale("x", { min: stateRef.current.range.start, max: stateRef.current.range.end });
  }, [thresholdKey, marksKey, annotationsKey, unitPrefsKey]);

  return <div className="chart-host" ref={hostRef} />;
}

function drawAlarmMarkers(stateRef: { current: { series: SeriesSpec[] } }) {
  return (u: uPlot) => {
    const { series } = stateRef.current;
    if (!series.some((s) => s.thresholds && s.thresholds.length > 0)) return;
    const ctx = u.ctx;
    const pxRatio = uPlot.pxRatio || 1;
    ctx.save();
    ctx.fillStyle = ALARM_COLOR;
    series.forEach((s, i) => {
      const conditions = s.thresholds;
      if (!conditions || conditions.length === 0) return;
      const xs = u.data[0];
      const ys = u.data[i + 1] as (number | null | undefined)[] | undefined;
      if (!xs || !ys) return;
      const scaleKey = `y-${s.unit || "·"}`;
      for (let j = 0; j < ys.length; j++) {
        const v = ys[j];
        const t = xs[j];
        if (v == null || t == null) continue;
        const breach = conditions.some((c) => (c.op === "gt" ? v > c.value : v < c.value));
        if (!breach) continue;
        const px = u.valToPos(t, "x", true);
        const py = u.valToPos(v, scaleKey, true);
        ctx.beginPath();
        ctx.arc(px, py, 2.5 * pxRatio, 0, 2 * Math.PI);
        ctx.fill();
      }
    });
    ctx.restore();
  };
}

function drawLogMarks(stateRef: { current: { marks?: number[] } }) {
  return (u: uPlot) => {
    const marks = stateRef.current.marks;
    if (!marks || marks.length === 0) return;
    const ctx = u.ctx;
    const pxRatio = uPlot.pxRatio || 1;
    ctx.save();
    ctx.strokeStyle = MARK_COLOR;
    ctx.lineWidth = 1 * pxRatio;
    ctx.setLineDash([4 * pxRatio, 3 * pxRatio]);
    for (const t of marks) {
      if (t < u.scales.x.min! || t > u.scales.x.max!) continue;
      const px = Math.round(u.valToPos(t, "x", true));
      ctx.beginPath();
      ctx.moveTo(px, u.bbox.top);
      ctx.lineTo(px, u.bbox.top + u.bbox.height);
      ctx.stroke();
    }
    ctx.restore();
  };
}

function drawAnnotations(stateRef: { current: { annotations?: { time: number; text: string }[] } }) {
  return (u: uPlot) => {
    const annotations = stateRef.current.annotations;
    if (!annotations || annotations.length === 0) return;
    const ctx = u.ctx;
    const pxRatio = uPlot.pxRatio || 1;
    ctx.save();
    ctx.strokeStyle = ANNOTATION_COLOR;
    ctx.fillStyle = ANNOTATION_COLOR;
    ctx.lineWidth = 1 * pxRatio;
    ctx.setLineDash([2 * pxRatio, 2 * pxRatio]);
    ctx.font = `${11 * pxRatio}px -apple-system, sans-serif`;
    ctx.textBaseline = "top";
    for (const a of annotations) {
      if (a.time < u.scales.x.min! || a.time > u.scales.x.max!) continue;
      const px = Math.round(u.valToPos(a.time, "x", true));
      ctx.beginPath();
      ctx.moveTo(px, u.bbox.top);
      ctx.lineTo(px, u.bbox.top + u.bbox.height);
      ctx.stroke();
      ctx.fillText(a.text, px + 4 * pxRatio, u.bbox.top + 2 * pxRatio);
    }
    ctx.restore();
  };
}

function formatTick(v: number): string {
  const abs = Math.abs(v);
  const digits = abs >= 100 ? 0 : abs >= 10 ? 1 : 2;
  return String(Number(v.toFixed(digits)));
}

function nearestIndex(xs: ArrayLike<number | null | undefined>, t: number): number {
  let best = 0;
  let bestDist = Infinity;
  for (let i = 0; i < xs.length; i++) {
    const x = xs[i];
    if (x == null) continue;
    const d = Math.abs(x - t);
    if (d < bestDist) {
      bestDist = d;
      best = i;
    }
  }
  return best;
}

function clamp(r: TimeRange, full: TimeRange, preserveSpan = false): TimeRange {
  let { start, end } = r;
  if (preserveSpan) {
    const span = end - start;
    if (start < full.start) [start, end] = [full.start, full.start + span];
    if (end > full.end) [start, end] = [full.end - span, full.end];
  }
  start = Math.max(start, full.start);
  end = Math.min(end, full.end);
  if (end - start < MIN_SPAN_S) end = start + MIN_SPAN_S;
  return { start, end };
}
