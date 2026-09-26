import * as echarts from "echarts";
import { useEffect, useMemo, useRef, useState } from "react";
import type { ChannelDto, FilterDto, GetHistogramRequest, GetHistogramResult } from "./protocol";
import { CHART_THEME } from "./theme";
import { DEFAULT_UNIT_PREFS, displayLabel, displayValue, type UnitPrefs } from "./units";

export interface HistogramViewProps {
  channels: ChannelDto[];
  filters: FilterDto[];
  fetchHistogram: (req: Omit<GetHistogramRequest, "logId">) => Promise<GetHistogramResult>;
  unitPrefs?: UnitPrefs;
  resolvedDark?: boolean;
}

const AGGS = ["mean", "min", "max", "count"] as const;

export function HistogramView({
  channels,
  filters,
  fetchHistogram,
  unitPrefs = DEFAULT_UNIT_PREFS,
  resolvedDark = true,
}: HistogramViewProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<echarts.ECharts | null>(null);

  const defaultId = (prefix: string, fallback: number) =>
    channels.find((c) => c.name.startsWith(prefix))?.id ?? fallback;

  const [xId, setXId] = useState(() => defaultId("Engine Speed", channels[0]?.id ?? 0));
  const [yId, setYId] = useState(() => defaultId("Engine Load", channels[0]?.id ?? 0));
  const [valueId, setValueId] = useState(() =>
    defaultId("Gauge Boost", defaultId("Boost Pressure Actual", channels[0]?.id ?? 0)),
  );
  const [agg, setAgg] = useState<(typeof AGGS)[number]>("mean");
  const [xBins, setXBins] = useState(16);
  const [yBins, setYBins] = useState(12);
  const [minCount, setMinCount] = useState(3);
  const [error, setError] = useState<string | null>(null);

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
    fetchHistogram({ xChannelId: xId, yChannelId: yId, valueChannelId: valueId, xBins, yBins, agg, minCount, filters })
      .then(({ histogram }) => {
        if (stale || !chartRef.current) return;
        setError(null);

        const xLabels = centers(histogram.xEdges, (v) => conv(v, xId));
        const yLabels = centers(histogram.yEdges, (v) => conv(v, yId));
        const data: [number, number, number][] = [];
        let min = Infinity;
        let max = -Infinity;
        histogram.cells.forEach((row, yi) =>
          row.forEach((cell, xi) => {
            if (cell === null) return;
            const v = conv(cell, valueId);
            data.push([xi, yi, v]);
            if (v < min) min = v;
            if (v > max) max = v;
          }),
        );
        if (data.length === 0) {
          min = 0;
          max = 1;
        }

        const t = resolvedDark ? CHART_THEME.dark : CHART_THEME.light;
        const axisStyle = {
          axisLabel: { color: t.axisStroke, fontSize: 10 },
          axisLine: { lineStyle: { color: t.gridStroke } },
          splitLine: { show: false },
        };
        chartRef.current.setOption(
          {
            backgroundColor: "transparent",
            grid: { left: 64, right: 100, top: 24, bottom: 46 },
            tooltip: {
              backgroundColor: t.tooltipBg,
              borderColor: t.tooltipBorder,
              textStyle: { color: t.tooltipText, fontSize: 12 },
              formatter: (p: { data: [number, number, number] }) => {
                const [xi, yi, v] = p.data;
                const count = histogram.counts[yi]?.[xi] ?? 0;
                return `${label(xId)}: <b>${xLabels[xi]}</b><br/>${label(yId)}: <b>${yLabels[yi]}</b><br/>${label(valueId)} (${agg}): <b>${v.toFixed(2)}</b><br/>samples: ${count}`;
              },
            },
            xAxis: { type: "category", data: xLabels, name: label(xId), nameLocation: "middle", nameGap: 30, nameTextStyle: { color: t.axisStroke }, ...axisStyle },
            yAxis: { type: "category", data: yLabels, name: label(yId), nameLocation: "middle", nameGap: 46, nameTextStyle: { color: t.axisStroke }, ...axisStyle },
            visualMap: {
              min,
              max,
              calculable: true,
              orient: "vertical",
              right: 8,
              top: "center",
              textStyle: { color: t.axisStroke },
              inRange: {
                color: t.gradient,
              },
            },
            series: [
              {
                type: "heatmap",
                data,
                label: {
                  show: xBins * yBins <= 400,
                  color: t.seriesLabel,
                  fontSize: 9,
                  formatter: (p: { data: [number, number, number] }) => formatCell(p.data[2]),
                },
                itemStyle: { borderColor: t.itemBorder, borderWidth: 1 },
                emphasis: { itemStyle: { borderColor: t.itemBorderEmphasis } },
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
  }, [fetchHistogram, xId, yId, valueId, agg, xBins, yBins, minCount, filters, label, unitPrefs, unitOf, resolvedDark]);

  const channelSelect = (value: number, onChange: (id: number) => void) => (
    <select value={value} onChange={(e) => onChange(Number(e.target.value))}>
      {channels.map((c) => (
        <option key={c.id} value={c.id}>
          {(c.computed ? "ƒ " : "") + displayLabel(c.name, c.unit, unitPrefs)}
        </option>
      ))}
    </select>
  );

  return (
    <div className="histogram-view">
      <div className="histogram-controls">
        <label>Value {channelSelect(valueId, setValueId)}</label>
        <label>
          Agg{" "}
          <select value={agg} onChange={(e) => setAgg(e.target.value as (typeof AGGS)[number])}>
            {AGGS.map((a) => (
              <option key={a}>{a}</option>
            ))}
          </select>
        </label>
        <label>X {channelSelect(xId, setXId)}</label>
        <label>Y {channelSelect(yId, setYId)}</label>
        <label>
          Bins{" "}
          <input type="number" min={2} max={64} value={xBins} onChange={(e) => setXBins(Number(e.target.value) || 16)} />
          ×
          <input type="number" min={2} max={64} value={yBins} onChange={(e) => setYBins(Number(e.target.value) || 12)} />
        </label>
        <label>
          Min samples{" "}
          <input type="number" min={1} value={minCount} onChange={(e) => setMinCount(Number(e.target.value) || 1)} />
        </label>
        {error && <span className="math-error">{error}</span>}
      </div>
      <div className="histogram-host" ref={hostRef} />
    </div>
  );
}

function centers(edges: number[], convert: (v: number) => number): string[] {
  const labels: string[] = [];
  for (let i = 0; i + 1 < edges.length; i++) {
    labels.push(formatCell(convert((edges[i] + edges[i + 1]) / 2)));
  }
  return labels;
}

function formatCell(v: number): string {
  const abs = Math.abs(v);
  if (abs >= 1000) return v.toFixed(0);
  if (abs >= 100) return v.toFixed(0);
  if (abs >= 10) return v.toFixed(1);
  return v.toFixed(2);
}
