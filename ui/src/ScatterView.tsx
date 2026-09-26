import * as echarts from "echarts";
import { useEffect, useMemo, useRef, useState } from "react";
import type { ChannelDto, FilterDto, GetScatterRequest, GetScatterResult, TimeRangeDto } from "./protocol";
import { CHART_THEME } from "./theme";
import { DEFAULT_UNIT_PREFS, displayLabel, displayValue, type UnitPrefs } from "./units";

export interface ScatterViewProps {
  channels: ChannelDto[];
  range: TimeRangeDto;
  fullRange: TimeRangeDto;
  filters: FilterDto[];
  fetchScatter: (req: Omit<GetScatterRequest, "logId">) => Promise<GetScatterResult>;
  unitPrefs?: UnitPrefs;
  resolvedDark?: boolean;
}

const NONE = -1;
const MAX_POINTS = 4000;

export function ScatterView({
  channels,
  range,
  fullRange,
  filters,
  fetchScatter,
  unitPrefs = DEFAULT_UNIT_PREFS,
  resolvedDark = true,
}: ScatterViewProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<echarts.ECharts | null>(null);

  const defaultId = (prefix: string, fallback: number) =>
    channels.find((c) => c.name.startsWith(prefix))?.id ?? fallback;

  const [xId, setXId] = useState(() => defaultId("Engine Speed", channels[0]?.id ?? 0));
  const [yId, setYId] = useState(() =>
    defaultId("Boost Error", defaultId("Boost Pressure Actual", channels[0]?.id ?? 0)),
  );
  const [colorId, setColorId] = useState<number>(() => channels.find((c) => c.name.startsWith("Gear"))?.id ?? NONE);
  const [useFullRange, setUseFullRange] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [totalSamples, setTotalSamples] = useState(0);
  const [plotted, setPlotted] = useState(0);

  const unitOf = useMemo(() => {
    const map = new Map<number, string>();
    for (const c of channels) map.set(c.id, c.unit);
    return (id: number) => map.get(id) ?? "";
  }, [channels]);

  const label = useMemo(() => {
    const map = new Map<number, string>();
    for (const c of channels) map.set(c.id, displayLabel(c.name, c.unit, unitPrefs));
    return (id: number) => map.get(id) ?? `#${id}`;
  }, [channels, unitPrefs]);

  const conv = (v: number, id: number) => displayValue(v, unitOf(id), unitPrefs).value;

  useEffect(() => {
    const host = hostRef.current;
    if (!host) return;
    const chart = echarts.init(host);
    chartRef.current = chart;
    const ro = new ResizeObserver(() => chart.resize());
    ro.observe(host);
    return () => {
      ro.disconnect();
      chart.dispose();
      chartRef.current = null;
    };
  }, []);

  useEffect(() => {
    let stale = false;
    const t0 = useFullRange ? fullRange.start : range.start;
    const t1 = useFullRange ? fullRange.end : range.end;
    fetchScatter({
      xChannelId: xId,
      yChannelId: yId,
      colorChannelId: colorId === NONE ? null : colorId,
      t0,
      t1,
      maxPoints: MAX_POINTS,
      filters,
    })
      .then(({ scatter }) => {
        if (stale || !chartRef.current) return;
        setError(null);
        setTotalSamples(scatter.totalSamples);
        setPlotted(scatter.x.length);

        const hasColor = colorId !== NONE;
        const data: [number, number, number | null][] = scatter.x.map((x, i) => [x, scatter.y[i], scatter.v[i]]);

        const t = resolvedDark ? CHART_THEME.dark : CHART_THEME.light;
        const axisStyle = {
          axisLabel: { color: t.axisStroke, fontSize: 10 },
          axisLine: { lineStyle: { color: t.gridStroke } },
          splitLine: { lineStyle: { color: t.splitLine } },
        };
        chartRef.current.setOption(
          {
            backgroundColor: "transparent",
            grid: { left: 64, right: hasColor ? 90 : 24, top: 24, bottom: 46 },
            tooltip: {
              backgroundColor: t.tooltipBg,
              borderColor: t.tooltipBorder,
              textStyle: { color: t.tooltipText, fontSize: 12 },
              formatter: (p: { data: [number, number, number | null] }) => {
                const [x, y, v] = p.data;
                const colorLine =
                  hasColor && v !== null ? `<br/>${label(colorId)}: <b>${conv(v, colorId).toFixed(2)}</b>` : "";
                return `${label(xId)}: <b>${conv(x, xId).toFixed(2)}</b><br/>${label(yId)}: <b>${conv(y, yId).toFixed(2)}</b>${colorLine}`;
              },
            },
            xAxis: {
              type: "value",
              name: label(xId),
              nameLocation: "middle",
              nameGap: 30,
              nameTextStyle: { color: t.axisStroke },
              scale: true,
              ...axisStyle,
              axisLabel: { ...axisStyle.axisLabel, formatter: (v: number) => conv(v, xId).toFixed(1) },
            },
            yAxis: {
              type: "value",
              name: label(yId),
              nameLocation: "middle",
              nameGap: 46,
              nameTextStyle: { color: t.axisStroke },
              scale: true,
              ...axisStyle,
              axisLabel: { ...axisStyle.axisLabel, formatter: (v: number) => conv(v, yId).toFixed(1) },
            },
            visualMap: hasColor
              ? {
                  dimension: 2,
                  min: min3(scatter.v),
                  max: max3(scatter.v),
                  text: [conv(max3(scatter.v), colorId).toFixed(1), conv(min3(scatter.v), colorId).toFixed(1)],
                  calculable: true,
                  orient: "vertical",
                  right: 8,
                  top: "center",
                  textStyle: { color: t.axisStroke },
                  inRange: {
                    color: t.gradient,
                  },
                }
              : undefined,
            series: [
              {
                type: "scatter",
                data,
                symbolSize: 4,
                itemStyle: hasColor ? undefined : { color: t.scatterFallback, opacity: 0.65 },
              },
            ],
          },
          true,
        );
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : String(err)));
    return () => {
      stale = true;
    };
  }, [fetchScatter, xId, yId, colorId, useFullRange, range, fullRange, filters, label, unitPrefs, unitOf, resolvedDark]);

  const channelSelect = (value: number, onChange: (id: number) => void, allowNone = false) => (
    <select value={value} onChange={(e) => onChange(Number(e.target.value))}>
      {allowNone && <option value={NONE}>none</option>}
      {channels.map((c) => (
        <option key={c.id} value={c.id}>
          {(c.computed ? "ƒ " : "") + displayLabel(c.name, c.unit, unitPrefs)}
        </option>
      ))}
    </select>
  );

  return (
    <div className="scatter-view">
      <div className="scatter-controls">
        <label>X {channelSelect(xId, setXId)}</label>
        <label>Y {channelSelect(yId, setYId)}</label>
        <label>Color {channelSelect(colorId, setColorId, true)}</label>
        <label>
          <input type="checkbox" checked={useFullRange} onChange={(e) => setUseFullRange(e.target.checked)} />
          Full log (not just current view)
        </label>
        <span className="scatter-count">
          {plotted.toLocaleString()} of {totalSamples.toLocaleString()} samples plotted
        </span>
        {error && <span className="math-error">{error}</span>}
      </div>
      <div className="scatter-host" ref={hostRef} />
    </div>
  );
}

function min3(v: (number | null)[]): number {
  let m = Infinity;
  for (const x of v) if (x !== null && x < m) m = x;
  return m === Infinity ? 0 : m;
}

function max3(v: (number | null)[]): number {
  let m = -Infinity;
  for (const x of v) if (x !== null && x > m) m = x;
  return m === -Infinity ? 1 : m;
}
