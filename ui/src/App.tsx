import {
  Fragment,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type DragEvent,
  type MouseEvent as ReactMouseEvent,
} from "react";
import type uPlot from "uplot";
import { Chart, type SeriesSpec } from "./Chart";
import { EngineClient } from "./engine";
import { FilterBar } from "./FilterBar";
import { HistogramView } from "./HistogramView";
import { Icon, type IconName } from "./icons";
import { AnnotationsPanel } from "./AnnotationsPanel";
import { LayoutBar } from "./LayoutBar";
import { LogInfoModal } from "./LogInfoModal";
import { MathPanel } from "./MathPanel";
import { ReplayView } from "./ReplayView";
import { ScatterView } from "./ScatterView";
import { SelectionStatsPanel } from "./SelectionStatsPanel";
import {
  applyThemePref,
  loadThemePref,
  resolveIsDark,
  storeThemePref,
  watchResolvedTheme,
  type ThemePref,
} from "./theme";
import { DEFAULT_UNIT_PREFS, displayLabel, displayUnit, type UnitPrefs } from "./units";
import type {
  AnnotationDto,
  ChannelDto,
  ChannelStatsDto,
  CustomMathChannelDto,
  FilterDto,
  GetHistogramRequest,
  GetRawSeriesRequest,
  GetScatterRequest,
  LayoutDto,
  MathPresetDto,
  OpenLogResult,
  RecentLogEntryDto,
  TimeRangeDto,
} from "./protocol";

type Status =
  | { kind: "connecting" }
  | { kind: "ready" }
  | { kind: "error"; message: string };

export interface Pane {
  id: number;
  channelIds: number[];
  yLocked?: boolean;
}

interface TabSnapshot {
  log: OpenLogResult;
  panes: Pane[];
  activePaneId: number;
  nextPaneId: number;
  paneGrow: Record<number, number>;
  range: TimeRangeDto | null;
  dataByPane: Record<number, uPlot.AlignedData>;
  filter: string;
  presets: MathPresetDto[];
  mathError: string | null;
  showCustomMath: boolean;
  view: "charts" | "histogram" | "scatter" | "replay";
  filters: FilterDto[];
  selectionStats: { range: TimeRangeDto; stats: ChannelStatsDto[] } | null;
  thresholds: Map<number, { op: "gt" | "lt"; value: number }[]>;
  thresholdDraft: { channelId: number; op: "gt" | "lt"; value: string } | null;
  marks: number[];
  annotations: AnnotationDto[];
  showLogInfo: boolean;
  showExportMenu: boolean;
  compare: OpenLogResult | null;
  compareOffset: number;
}

const PALETTE = [
  "#4cc2ff", "#ffb454", "#7ee787", "#ff7b72",
  "#d2a8ff", "#f778ba", "#a5d6ff", "#ffd33d",
];

const COLORBLIND_PALETTE = [
  "#ffffff", "#e69f00", "#56b4e9", "#009e73",
  "#f0e442", "#0072b2", "#d55e00", "#cc79a7",
];

const CHANNEL_GROUPS: { key: string; label: string; icon: IconName; test: RegExp }[] = [
  { key: "boost", label: "Boost", icon: "boost", test: /boost/i },
  { key: "fuel", label: "Fuel", icon: "fuel", test: /fuel|ethanol|afr|lambda/i },
  { key: "ignition", label: "Ignition", icon: "ignition", test: /ignition|knock|spark/i },
  { key: "electrical", label: "Electrical / CAN", icon: "can", test: /\bcan\b|volt|battery|alternator/i },
  { key: "engine", label: "Engine", icon: "engine", test: /engine|accelerator|gear|rpm|throttle|clutch/i },
  { key: "environment", label: "Environment", icon: "misc", test: /ambient|barometric|weather/i },
];
const OTHER_GROUP = { key: "other", label: "Other", icon: "misc" as IconName };
const MATH_GROUP = { key: "math", label: "Math channels", icon: "line" as IconName };
const GROUP_ORDER = [MATH_GROUP.key, ...CHANNEL_GROUPS.map((g) => g.key), OTHER_GROUP.key];

function groupForChannel(c: ChannelDto): { key: string; label: string; icon: IconName } {
  if (c.computed) return MATH_GROUP;
  return CHANNEL_GROUPS.find((g) => g.test.test(c.name)) ?? OTHER_GROUP;
}

export function App() {
  const clientRef = useRef<EngineClient | null>(null);
  const chartColumnRef = useRef<HTMLElement>(null);
  const [status, setStatus] = useState<Status>({ kind: "connecting" });
  const [log, setLog] = useState<OpenLogResult | null>(null);
  const [tabs, setTabs] = useState<OpenLogResult[]>([]);
  const tabSnapshots = useRef(new Map<number, TabSnapshot>());
  const [panes, setPanes] = useState<Pane[]>([{ id: 1, channelIds: [] }]);
  const [activePaneId, setActivePaneId] = useState(1);
  const [paneGrow, setPaneGrow] = useState<Record<number, number>>({});
  const nextPaneId = useRef(2);
  const [range, setRange] = useState<TimeRangeDto | null>(null);
  const [width, setWidth] = useState(800);
  const [dataByPane, setDataByPane] = useState<Record<number, uPlot.AlignedData>>({});
  const [filter, setFilter] = useState("");
  const [presets, setPresets] = useState<MathPresetDto[]>([]);
  const [mathError, setMathError] = useState<string | null>(null);
  const [showCustomMath, setShowCustomMath] = useState(false);
  const [view, setView] = useState<"charts" | "histogram" | "scatter" | "replay">("charts");
  const [filters, setFilters] = useState<FilterDto[]>([]);
  const [selectionStats, setSelectionStats] = useState<{
    range: TimeRangeDto;
    stats: ChannelStatsDto[];
  } | null>(null);
  const [thresholds, setThresholds] = useState<Map<number, { op: "gt" | "lt"; value: number }[]>>(
    new Map(),
  );
  const [thresholdDraft, setThresholdDraft] = useState<{
    channelId: number;
    op: "gt" | "lt";
    value: string;
  } | null>(null);
  const [marks, setMarks] = useState<number[]>([]);
  const [annotations, setAnnotations] = useState<AnnotationDto[]>([]);
  const [unitPrefs, setUnitPrefs] = useState<UnitPrefs>(DEFAULT_UNIT_PREFS);
  const [colorblindMode, setColorblindMode] = useState(false);
  const palette = colorblindMode ? COLORBLIND_PALETTE : PALETTE;
  const [themePref, setThemePref] = useState<ThemePref>(() => loadThemePref());
  const [resolvedDark, setResolvedDark] = useState(() => resolveIsDark(themePref));
  useEffect(() => {
    applyThemePref(themePref);
    storeThemePref(themePref);
    setResolvedDark(resolveIsDark(themePref));
    return watchResolvedTheme(themePref, setResolvedDark);
  }, [themePref]);
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());
  const toggleGroupCollapsed = (key: string) =>
    setCollapsedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  const [showLogInfo, setShowLogInfo] = useState(false);
  const [layouts, setLayouts] = useState<LayoutDto[]>([]);
  const [channelColors, setChannelColors] = useState<Record<string, string>>({});
  const [recentLogs, setRecentLogs] = useState<RecentLogEntryDto[]>([]);
  const [customMathChannels, setCustomMathChannels] = useState<CustomMathChannelDto[]>([]);
  const [showRecentMenu, setShowRecentMenu] = useState(false);
  const [isDragOver, setIsDragOver] = useState(false);
  const [compare, setCompare] = useState<OpenLogResult | null>(null);
  const [compareOffset, setCompareOffset] = useState(0);
  const [showExportMenu, setShowExportMenu] = useState(false);

  useEffect(() => {
    const client = new EngineClient();
    clientRef.current = client;
    client.onclose = () => setStatus({ kind: "error", message: "Engine connection lost" });
    window.olv
      .engineUrl()
      .then((url) => client.connect(url))
      .then(() => {
        setStatus({ kind: "ready" });
        return client.request("listLayouts", {});
      })
      .then(({ layouts }) => setLayouts(layouts))
      .then(() => client.request("listChannelColors", {}))
      .then(({ colors }) => setChannelColors(colors))
      .then(() => client.request("listRecentLogs", {}))
      .then(({ logs }) => setRecentLogs(logs))
      .then(() => client.request("listCustomMathChannels", {}))
      .then(({ channels }) => setCustomMathChannels(channels))
      .catch((err: unknown) => setStatus({ kind: "error", message: String(err) }));
  }, []);

  function captureSnapshot(): TabSnapshot | null {
    if (!log) return null;
    return {
      log,
      panes,
      activePaneId,
      nextPaneId: nextPaneId.current,
      paneGrow,
      range,
      dataByPane,
      filter,
      presets,
      mathError,
      showCustomMath,
      view,
      filters,
      selectionStats,
      thresholds,
      thresholdDraft,
      marks,
      annotations,
      showLogInfo,
      showExportMenu,
      compare,
      compareOffset,
    };
  }

  function restoreSnapshot(snap: TabSnapshot) {
    setLog(snap.log);
    setPanes(snap.panes);
    setActivePaneId(snap.activePaneId);
    nextPaneId.current = snap.nextPaneId;
    setPaneGrow(snap.paneGrow);
    setRange(snap.range);
    setDataByPane(snap.dataByPane);
    setFilter(snap.filter);
    setPresets(snap.presets);
    setMathError(snap.mathError);
    setShowCustomMath(snap.showCustomMath);
    setView(snap.view);
    setFilters(snap.filters);
    setSelectionStats(snap.selectionStats);
    setThresholds(snap.thresholds);
    setThresholdDraft(snap.thresholdDraft);
    setMarks(snap.marks);
    setAnnotations(snap.annotations);
    setShowLogInfo(snap.showLogInfo);
    setShowExportMenu(snap.showExportMenu);
    setCompare(snap.compare);
    setCompareOffset(snap.compareOffset);
  }

  async function applyLog(opened: OpenLogResult) {
    const client = clientRef.current;
    if (!client) return;
    setLog(opened);
    setRange(opened.timeRange);
    setDataByPane({});
    setPanes(defaultPanes(opened.channels));
    setActivePaneId(1);
    nextPaneId.current = 3;
    setMathError(null);
    setFilters([]);
    setSelectionStats(null);
    setThresholds(new Map());
    setThresholdDraft(null);
    setMarks([]);
    setAnnotations([]);
    setShowLogInfo(false);
    setShowExportMenu(false);
    if (compare) {
      void client.request("closeLog", { logId: compare.logId }).catch(() => {});
      setCompare(null);
      setCompareOffset(0);
    }
    try {
      const { presets } = await client.request("listMathPresets", { logId: opened.logId });
      setPresets(presets);
      const { times } = await client.request("listMarks", { logId: opened.logId });
      setMarks(times);
      const { annotations: notes } = await client.request("listAnnotations", { logId: opened.logId });
      setAnnotations(notes);
    } catch (err) {
      setStatus({ kind: "error", message: String(err) });
    }
  }

  async function openNewTab(path: string) {
    const client = clientRef.current;
    if (!client) return;
    try {
      const opened = await client.request("openLog", { path });
      if (log) {
        const outgoing = captureSnapshot();
        if (outgoing) tabSnapshots.current.set(log.logId, outgoing);
      }
      setTabs((prev) => [...prev, opened]);
      await applyLog(opened);
      const { logs } = await client.request("listRecentLogs", {});
      setRecentLogs(logs);
    } catch (err) {
      setStatus({ kind: "error", message: String(err) });
    }
  }

  function switchTab(logId: number) {
    if (!log || logId === log.logId) return;
    const target = tabs.find((t) => t.logId === logId);
    if (!target) return;

    const outgoing = captureSnapshot();
    if (outgoing) tabSnapshots.current.set(log.logId, outgoing);

    const incoming = tabSnapshots.current.get(logId);
    if (incoming) restoreSnapshot(incoming);
    else void applyLog(target);
  }

  function closeTab(logId: number) {
    const client = clientRef.current;
    if (client) void client.request("closeLog", { logId }).catch(() => {});
    tabSnapshots.current.delete(logId);
    const remaining = tabs.filter((t) => t.logId !== logId);
    setTabs(remaining);
    if (log?.logId !== logId) return;

    if (client && compare) void client.request("closeLog", { logId: compare.logId }).catch(() => {});

    const next = remaining[remaining.length - 1];
    if (next) {
      const snap = tabSnapshots.current.get(next.logId);
      if (snap) restoreSnapshot(snap);
      else void applyLog(next);
      return;
    }
    setLog(null);
    setRange(null);
    setDataByPane({});
    setPanes([{ id: 1, channelIds: [] }]);
    setActivePaneId(1);
    nextPaneId.current = 2;
    setMathError(null);
    setFilters([]);
    setSelectionStats(null);
    setThresholds(new Map());
    setThresholdDraft(null);
    setMarks([]);
    setAnnotations([]);
    setShowLogInfo(false);
    setShowExportMenu(false);
    setCompare(null);
    setCompareOffset(0);
  }

  const openLog = useCallback(async () => {
    const path = await window.olv.openLogDialog();
    if (path) await openNewTab(path);
  }, [openNewTab]);

  const removeRecentLog = useCallback(async (path: string) => {
    const client = clientRef.current;
    if (!client) return;
    const { logs } = await client.request("removeRecentLog", { path });
    setRecentLogs(logs);
  }, []);

  const openComparePath = useCallback(
    async (path: string) => {
      const client = clientRef.current;
      if (!client || !log) return;
      try {
        const opened = await client.request("openLog", { path });
        if (compare) void client.request("closeLog", { logId: compare.logId }).catch(() => {});
        setCompare(opened);
        setCompareOffset(0);
      } catch (err) {
        setStatus({ kind: "error", message: String(err) });
      }
    },
    [log, compare],
  );

  const openCompare = useCallback(async () => {
    const path = await window.olv.openLogDialog();
    if (path) await openComparePath(path);
  }, [openComparePath]);

  const autoCompared = useRef(false);
  useEffect(() => {
    if (!log || autoCompared.current) return;
    autoCompared.current = true;
    void window.olv.initialComparePath().then((path) => (path ? openComparePath(path) : undefined));
  }, [log, openComparePath]);

  const closeCompare = useCallback(() => {
    const client = clientRef.current;
    if (compare && client) void client.request("closeLog", { logId: compare.logId }).catch(() => {});
    setCompare(null);
    setCompareOffset(0);
  }, [compare]);

  const autoOpened = useRef(false);
  useEffect(() => {
    if (status.kind !== "ready" || autoOpened.current) return;
    autoOpened.current = true;
    void window.olv.initialLogPath().then((path) => (path ? openNewTab(path) : undefined));
  }, [status, openNewTab]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "o") {
        e.preventDefault();
        void openLog();
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [openLog]);

  const channelById = useMemo(() => {
    const map = new Map<number, ChannelDto>();
    for (const c of log?.channels ?? []) map.set(c.id, c);
    return map;
  }, [log]);

  const compareIdByLabel = useMemo(() => {
    const map = new Map<string, number>();
    for (const c of compare?.channels ?? []) {
      const label = c.unit ? `${c.name} (${c.unit})` : c.name;
      if (!map.has(label)) map.set(label, c.id);
    }
    return map;
  }, [compare]);

  const paneSeries = useCallback(
    (pane: Pane): { specs: SeriesSpec[]; sources: { logId: number; channelId: number; offset: number }[] } => {
      const specs: SeriesSpec[] = [];
      const sources: { logId: number; channelId: number; offset: number }[] = [];
      if (!log) return { specs, sources };
      pane.channelIds.forEach((channelId, i) => {
        const c = channelById.get(channelId);
        const rawLabel = c ? (c.unit ? `${c.name} (${c.unit})` : c.name) : `#${channelId}`;
        const color = channelColors[rawLabel] ?? palette[i % palette.length];
        const label = c ? displayLabel(c.name, c.unit, unitPrefs) : rawLabel;
        specs.push({ channelId, label, unit: c?.unit ?? "", color, thresholds: thresholds.get(channelId) });
        sources.push({ logId: log.logId, channelId, offset: 0 });
        if (compare) {
          const compareId = compareIdByLabel.get(rawLabel);
          if (compareId !== undefined) {
            specs.push({
              channelId: -1000 - compareId,
              label: `${label} — B`,
              unit: c?.unit ?? "",
              color,
              dashed: true,
            });
            sources.push({ logId: compare.logId, channelId: compareId, offset: compareOffset });
          }
        }
      });
      return { specs, sources };
    },
    [log, compare, compareOffset, channelById, compareIdByLabel, thresholds, unitPrefs, channelColors, palette],
  );

  const addThresholdCondition = useCallback(() => {
    if (!thresholdDraft) return;
    const value = Number.parseFloat(thresholdDraft.value);
    if (Number.isNaN(value)) return;
    setThresholds((prev) => {
      const next = new Map(prev);
      const existing = next.get(thresholdDraft.channelId) ?? [];
      if (existing.some((c) => c.op === thresholdDraft.op && c.value === value)) return prev;
      next.set(thresholdDraft.channelId, [...existing, { op: thresholdDraft.op, value }]);
      return next;
    });
    setThresholdDraft((d) => (d ? { ...d, value: "" } : d));
  }, [thresholdDraft]);

  const removeThresholdCondition = useCallback((channelId: number, index: number) => {
    setThresholds((prev) => {
      const existing = prev.get(channelId);
      if (!existing) return prev;
      const next = new Map(prev);
      const remaining = existing.filter((_, i) => i !== index);
      if (remaining.length === 0) next.delete(channelId);
      else next.set(channelId, remaining);
      return next;
    });
  }, []);

  const requestSeq = useRef(0);
  useEffect(() => {
    const client = clientRef.current;
    if (!client || !log || !range) return;
    const seq = ++requestSeq.current;
    const timer = setTimeout(() => {
      for (const pane of panes) {
        const { sources } = paneSeries(pane);
        if (sources.length === 0) continue;
        client
          .request("getOverlaySeries", {
            sources,
            t0: range.start,
            t1: range.end,
            pixelWidth: Math.max(Math.round(width * (window.devicePixelRatio || 1)), 100),
          })
          .then((res) => {
            if (seq !== requestSeq.current) return;
            const data = [res.t, ...res.series.map((s) => s.v)] as uPlot.AlignedData;
            setDataByPane((prev) => ({ ...prev, [pane.id]: data }));
          })
          .catch(() => {});
      }
    }, 40);
    return () => clearTimeout(timer);
  }, [log, panes, range, width, paneSeries]);

  const visibleChannels = useMemo(() => {
    if (!log) return [];
    const q = filter.trim().toLowerCase();
    return q ? log.channels.filter((c) => c.name.toLowerCase().includes(q)) : log.channels;
  }, [log, filter]);

  const groupedChannels = useMemo(() => {
    const buckets = new Map<string, { label: string; icon: IconName; channels: ChannelDto[] }>();
    for (const c of visibleChannels) {
      const g = groupForChannel(c);
      if (!buckets.has(g.key)) buckets.set(g.key, { label: g.label, icon: g.icon, channels: [] });
      buckets.get(g.key)!.channels.push(c);
    }
    return GROUP_ORDER.map((key) => {
      const bucket = buckets.get(key);
      return bucket ? { key, ...bucket } : null;
    }).filter((b): b is { key: string; label: string; icon: IconName; channels: ChannelDto[] } => b !== null);
  }, [visibleChannels]);

  const activePane = panes.find((p) => p.id === activePaneId) ?? panes[0];

  const toggleChannel = (channelId: number) => {
    setPanes((prev) =>
      prev.map((p) =>
        p.id === activePane.id
          ? {
              ...p,
              channelIds: p.channelIds.includes(channelId)
                ? p.channelIds.filter((id) => id !== channelId)
                : [...p.channelIds, channelId],
            }
          : p,
      ),
    );
  };

  const CHANNEL_DRAG_TYPE = "application/x-olv-channel-id";
  const [dragOverPaneId, setDragOverPaneId] = useState<number | null>(null);
  const addChannelToPane = useCallback((paneId: number, channelId: number) => {
    setPanes((prev) =>
      prev.map((p) =>
        p.id === paneId && !p.channelIds.includes(channelId)
          ? { ...p, channelIds: [...p.channelIds, channelId] }
          : p,
      ),
    );
  }, []);

  const removeFromPane = (paneId: number, channelId: number) => {
    setPanes((prev) =>
      prev.map((p) =>
        p.id === paneId ? { ...p, channelIds: p.channelIds.filter((id) => id !== channelId) } : p,
      ),
    );
  };

  const addPane = () => {
    const id = nextPaneId.current++;
    setPanes((prev) => [...prev, { id, channelIds: [] }]);
    setActivePaneId(id);
  };

  const startPaneResize = useCallback(
    (e: ReactMouseEvent, aboveId: number, belowId: number) => {
      e.preventDefault();
      const container = chartColumnRef.current;
      const aboveEl = container?.querySelector<HTMLElement>(`[data-pane-id="${aboveId}"]`);
      const belowEl = container?.querySelector<HTMLElement>(`[data-pane-id="${belowId}"]`);
      if (!aboveEl || !belowEl) return;

      const startY = e.clientY;
      const aboveStartHeight = aboveEl.getBoundingClientRect().height;
      const belowStartHeight = belowEl.getBoundingClientRect().height;
      const totalHeight = aboveStartHeight + belowStartHeight;
      const totalGrow = (paneGrow[aboveId] ?? 1) + (paneGrow[belowId] ?? 1);
      const minHeightPx = 120;

      const onMove = (moveEvent: MouseEvent) => {
        const rawHeight = aboveStartHeight + (moveEvent.clientY - startY);
        const clamped = Math.max(minHeightPx, Math.min(totalHeight - minHeightPx, rawHeight));
        const aboveGrow = totalGrow * (clamped / totalHeight);
        setPaneGrow((prev) => ({ ...prev, [aboveId]: aboveGrow, [belowId]: totalGrow - aboveGrow }));
      };
      const onUp = () => {
        window.removeEventListener("mousemove", onMove);
        window.removeEventListener("mouseup", onUp);
      };
      window.addEventListener("mousemove", onMove);
      window.addEventListener("mouseup", onUp);
    },
    [paneGrow],
  );

  const removePane = (paneId: number) => {
    setPanes((prev) => {
      const next = prev.filter((p) => p.id !== paneId);
      if (activePaneId === paneId && next.length > 0) setActivePaneId(next[0].id);
      setDataByPane((d) => {
        const { [paneId]: _removed, ...rest } = d;
        return rest;
      });
      return next.length > 0 ? next : prev;
    });
    setSelectionStats(null);
  };

  const addMathChannel = useCallback(
    async (name: string, unit: string, expression: string) => {
      const client = clientRef.current;
      if (!client || !log) return;
      setMathError(null);
      try {
        const { channel } = await client.request("addMathChannel", {
          logId: log.logId,
          name,
          unit,
          expression,
        });
        setLog((prev) => (prev ? { ...prev, channels: [...prev.channels, channel] } : prev));
        setPanes((prev) =>
          prev.map((p) => (p.id === activePaneId ? { ...p, channelIds: [...p.channelIds, channel.id] } : p)),
        );
        const { channels: savedChannels } = await client.request("saveCustomMathChannel", {
          name,
          unit,
          expression,
        });
        setCustomMathChannels(savedChannels);
        if (compare) {
          try {
            const mirrored = await client.request("addMathChannel", {
              logId: compare.logId,
              name,
              unit,
              expression,
            });
            setCompare((prev) =>
              prev ? { ...prev, channels: [...prev.channels, mirrored.channel] } : prev,
            );
          } catch {
          }
        }
      } catch (err) {
        setMathError(err instanceof Error ? err.message : String(err));
      }
    },
    [log, activePaneId, compare],
  );

  const deleteCustomMathChannel = useCallback(async (name: string) => {
    const client = clientRef.current;
    if (!client) return;
    const { channels } = await client.request("deleteCustomMathChannel", { name });
    setCustomMathChannels(channels);
  }, []);

  const fetchHistogram = useCallback(
    (req: Omit<GetHistogramRequest, "logId">) => {
      const client = clientRef.current;
      if (!client || !log) return Promise.reject(new Error("no log open"));
      return client.request("getHistogram", { ...req, logId: log.logId });
    },
    [log],
  );

  useEffect(() => {
    setSelectionStats(null);
  }, [filters]);

  const handleSelectionStats = useCallback(
    async (selRange: TimeRangeDto) => {
      const client = clientRef.current;
      const channelIds = [...new Set(panes.flatMap((p) => p.channelIds))];
      if (!client || !log || channelIds.length === 0) return;
      try {
        const { stats } = await client.request("getSelectionStats", {
          logId: log.logId,
          channelIds,
          t0: selRange.start,
          t1: selRange.end,
          filters,
        });
        setSelectionStats({ range: selRange, stats });
      } catch {
      }
    },
    [log, panes, filters],
  );

  const fetchScatter = useCallback(
    (req: Omit<GetScatterRequest, "logId">) => {
      const client = clientRef.current;
      if (!client || !log) return Promise.reject(new Error("no log open"));
      return client.request("getScatter", { ...req, logId: log.logId });
    },
    [log],
  );

  const fetchRawSeries = useCallback(
    (req: Omit<GetRawSeriesRequest, "logId">) => {
      const client = clientRef.current;
      if (!client || !log) return Promise.reject(new Error("no log open"));
      return client.request("getRawSeries", { ...req, logId: log.logId });
    },
    [log],
  );

  const addAnnotationAt = useCallback(
    async (time: number) => {
      const client = clientRef.current;
      if (!client || !log) return;
      const text = window.prompt(`Note at ${time.toFixed(2)}s:`)?.trim();
      if (!text) return;
      const { annotations: notes } = await client.request("addAnnotation", { logId: log.logId, time, text });
      setAnnotations(notes);
    },
    [log],
  );

  const deleteAnnotation = useCallback(
    async (id: string) => {
      const client = clientRef.current;
      if (!client || !log) return;
      const { annotations: notes } = await client.request("deleteAnnotation", { logId: log.logId, id });
      setAnnotations(notes);
    },
    [log],
  );

  const [exporting, setExporting] = useState(false);
  const exportCsv = useCallback(async () => {
    const client = clientRef.current;
    const channelIds = [...new Set(panes.flatMap((p) => p.channelIds))];
    if (!client || !log || !range || channelIds.length === 0) return;
    setExporting(true);
    try {
      const { csv } = await client.request("exportRange", {
        logId: log.logId,
        channelIds,
        t0: range.start,
        t1: range.end,
      });
      const stem = basename(log.sourcePath).replace(/\.[^.]+$/, "");
      await window.olv.saveTextFile(`${stem}_${range.start.toFixed(1)}-${range.end.toFixed(1)}s.csv`, csv);
    } finally {
      setExporting(false);
    }
  }, [log, panes, range]);

  const [exportingPng, setExportingPng] = useState(false);
  const exportPng = useCallback(async () => {
    const el = chartColumnRef.current;
    if (!el || !log) return;
    setExportingPng(true);
    try {
      const r = el.getBoundingClientRect();
      const stem = basename(log.sourcePath).replace(/\.[^.]+$/, "");
      await window.olv.saveChartPng(`${stem}_charts.png`, {
        x: Math.round(r.x),
        y: Math.round(r.y),
        width: Math.round(r.width),
        height: Math.round(r.height),
      });
    } finally {
      setExportingPng(false);
    }
  }, [log]);

  const channelLabel = useCallback((c: ChannelDto) => (c.unit ? `${c.name} (${c.unit})` : c.name), []);

  const applyLayout = useCallback(
    (layout: LayoutDto) => {
      if (!log) return;
      const idByLabel = new Map<string, number>();
      for (const c of log.channels) {
        const l = channelLabel(c);
        if (!idByLabel.has(l)) idByLabel.set(l, c.id);
      }
      const mapped: Pane[] = layout.panes.map((p, i) => ({
        id: i + 1,
        channelIds: p.channelLabels
          .map((l) => idByLabel.get(l))
          .filter((chId): chId is number => chId !== undefined),
      }));
      if (mapped.length === 0) return;
      setPanes(mapped);
      setActivePaneId(mapped[0].id);
      nextPaneId.current = mapped.length + 1;
    },
    [log, channelLabel],
  );

  const saveLayout = useCallback(
    async (name: string) => {
      const client = clientRef.current;
      if (!client || !log) return;
      const dto: LayoutDto = {
        name,
        panes: panes.map((p) => ({
          channelLabels: p.channelIds
            .map((chId) => channelById.get(chId))
            .filter((c): c is ChannelDto => !!c)
            .map(channelLabel),
        })),
      };
      const { layouts } = await client.request("saveLayout", dto);
      setLayouts(layouts);
    },
    [log, panes, channelById, channelLabel],
  );

  const deleteLayout = useCallback(async (name: string) => {
    const client = clientRef.current;
    if (!client) return;
    const { layouts } = await client.request("deleteLayout", { name });
    setLayouts(layouts);
  }, []);

  const setChannelColor = useCallback(async (label: string, color: string) => {
    const client = clientRef.current;
    if (!client) return;
    const { colors } = await client.request("setChannelColor", { label, color });
    setChannelColors(colors);
  }, []);

  const clearChannelColor = useCallback(async (label: string) => {
    const client = clientRef.current;
    if (!client) return;
    const { colors } = await client.request("clearChannelColor", { label });
    setChannelColors(colors);
  }, []);

  const tune = log?.metadata["Ecu programming comment"];
  const compareTune = compare?.metadata["Ecu programming comment"];

  const handleDragOver = useCallback((e: DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "copy";
    setIsDragOver(true);
  }, []);
  const handleDragLeave = useCallback((e: DragEvent) => {
    if (e.currentTarget.contains(e.relatedTarget as Node | null)) return;
    setIsDragOver(false);
  }, []);
  const handleDrop = useCallback(
    (e: DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      const file = e.dataTransfer.files[0];
      if (!file || status.kind !== "ready") return;
      const path = window.olv.pathForDroppedFile(file);
      if (e.shiftKey && log) void openComparePath(path);
      else void openNewTab(path);
    },
    [status, log, openNewTab, openComparePath],
  );

  return (
    <div
      className={`app${isDragOver ? " drag-over" : ""}`}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      {isDragOver && (
        <div className="drop-overlay">
          {log ? "Drop to open · Shift+drop to compare" : "Drop to open"}
        </div>
      )}
      <header className="toolbar toolbar-main">
        <button onClick={() => void openLog()} disabled={status.kind !== "ready"}>
          <Icon name="folder" />
          Open Log…
        </button>
        {recentLogs.length > 0 && (
          <div className="recent-menu">
            <button
              className="secondary"
              disabled={status.kind !== "ready"}
              onClick={() => setShowRecentMenu((v) => !v)}
            >
              <Icon name="clock" />
              Recent
              <Icon name="chevron" className="menu-chevron" />
            </button>
            {showRecentMenu && (
              <div className="recent-menu-list" onMouseLeave={() => setShowRecentMenu(false)}>
                {recentLogs.map((entry) => (
                  <div key={entry.path} className="recent-menu-item">
                    <button
                      disabled={!entry.exists}
                      title={entry.path}
                      onClick={() => {
                        setShowRecentMenu(false);
                        void openNewTab(entry.path);
                      }}
                    >
                      {basename(entry.path)}
                      {!entry.exists && <span className="recent-menu-missing"> (missing)</span>}
                    </button>
                    <button
                      className="chip-x"
                      title="Remove from recent logs"
                      onClick={() => void removeRecentLog(entry.path)}
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
        {log ? (
          <button className="log-id-block" title="Click for full log details" onClick={() => setShowLogInfo(true)}>
            <Icon name="engine" className="log-id-icon" />
            <span className="log-id-text">
              <span className="log-id-name">
                {basename(log.sourcePath)}
                <span className="log-title-info">ⓘ</span>
              </span>
              <span className="log-id-meta">
                {log.metadata["Vehicle"] ?? "unknown vehicle"}
                {tune ? ` · ${tune}` : ""} · {log.sampleCount.toLocaleString()} samples ·{" "}
                {(log.timeRange.end - log.timeRange.start).toFixed(1)} s
              </span>
            </span>
          </button>
        ) : (
          <span className="log-title-placeholder">No log open</span>
        )}
        {log && (
          <button className="icon-btn" title="Close log" onClick={() => closeTab(log.logId)}>
            <Icon name="close" />
          </button>
        )}
        {log && showLogInfo && (
          <LogInfoModal
            log={log}
            parserName={
              clientRef.current?.hello?.parsers.find((p) => p.id === log.parserId)?.displayName ?? log.parserId
            }
            onClose={() => setShowLogInfo(false)}
          />
        )}
        <span className="toolbar-divider" />
        <div className="settings-cluster">
          <span className="unit-compact">
            <button
              className="unit-chip"
              title="Toggle pressure unit"
              onClick={() => setUnitPrefs((p) => ({ ...p, pressure: p.pressure === "psi" ? "kPa" : "psi" }))}
            >
              {unitPrefs.pressure}
            </button>
            <button
              className="unit-chip"
              title="Toggle temperature unit"
              onClick={() => setUnitPrefs((p) => ({ ...p, temp: p.temp === "°F" ? "°C" : "°F" }))}
            >
              {unitPrefs.temp}
            </button>
            <button
              className="unit-chip"
              title="Toggle speed unit"
              onClick={() => setUnitPrefs((p) => ({ ...p, speed: p.speed === "mph" ? "km/h" : "mph" }))}
            >
              {unitPrefs.speed}
            </button>
            <button
              className="unit-chip"
              title="Toggle lambda/AFR (assumes gasoline stoich 14.7:1)"
              onClick={() => setUnitPrefs((p) => ({ ...p, lambda: p.lambda === "lambda" ? "AFR" : "lambda" }))}
            >
              {unitPrefs.lambda}
            </button>
          </span>
          <button
            className={`icon-btn${colorblindMode ? " on" : ""}`}
            title="Colorblind-safe channel palette (Wong 2011)"
            onClick={() => setColorblindMode((v) => !v)}
          >
            <Icon name="eye" />
          </button>
          <div className="seg-control theme-seg">
            <button
              className={`seg-btn${themePref === "system" ? " on" : ""}`}
              title="Match system"
              onClick={() => setThemePref("system")}
            >
              <Icon name="system" />
            </button>
            <button
              className={`seg-btn${themePref === "light" ? " on" : ""}`}
              title="Light"
              onClick={() => setThemePref("light")}
            >
              <Icon name="sun" />
            </button>
            <button
              className={`seg-btn${themePref === "dark" ? " on" : ""}`}
              title="Dark"
              onClick={() => setThemePref("dark")}
            >
              <Icon name="moon" />
            </button>
          </div>
        </div>
        <span className={`status status-${status.kind}`}>
          <span className="status-dot" />
          {status.kind === "connecting" && "connecting to engine…"}
          {status.kind === "ready" && "engine ready"}
          {status.kind === "error" && status.message}
        </span>
      </header>

      {tabs.length > 1 && (
        <div className="tab-bar">
          {tabs.map((tab) => (
            <div
              key={tab.logId}
              className={`tab-chip${tab.logId === log?.logId ? " active" : ""}`}
              title={tab.sourcePath}
              onClick={() => switchTab(tab.logId)}
            >
              {basename(tab.sourcePath)}
              <button
                className="chip-x"
                title="Close tab"
                onClick={(e) => {
                  e.stopPropagation();
                  closeTab(tab.logId);
                }}
              >
                ×
              </button>
            </div>
          ))}
        </div>
      )}

      {log && (
        <header className="toolbar toolbar-context">
          <div className="toolbar-context-row">
            <div className="seg-control view-seg">
              {(["charts", "histogram", "scatter", "replay"] as const).map((v) => (
                <button
                  key={v}
                  className={`seg-btn${view === v ? " on" : ""}`}
                  onClick={() => setView(v)}
                >
                  <Icon name={v === "charts" ? "line" : v === "histogram" ? "bars" : v === "scatter" ? "scatter" : "play"} />
                  {v === "charts"
                    ? "Charts"
                    : v === "histogram"
                      ? "Histogram"
                      : v === "scatter"
                        ? "Scatter"
                        : "Replay"}
                </button>
              ))}
            </div>

            {view === "charts" && (
              <>
                <span className="toolbar-divider" />
                <div className="icon-cluster">
                  <button
                    className={`icon-btn${compare ? " on" : ""}`}
                    title={compare ? "Comparing — click to replace with a different log" : "Compare against another log"}
                    onClick={() => void openCompare()}
                  >
                    <Icon name="compare" />
                  </button>
                  <span className="layout-icon-group" title="Layout profiles">
                    <Icon name="grid" className="layout-icon" />
                    <LayoutBar
                      layouts={layouts}
                      onApply={applyLayout}
                      onSave={(name) => void saveLayout(name)}
                      onDelete={(name) => void deleteLayout(name)}
                    />
                  </span>
                </div>
              </>
            )}

            <span className="toolbar-divider" />
            <div className="export-menu">
              <button className="icon-btn" title="Export" onClick={() => setShowExportMenu((v) => !v)}>
                <Icon name="export" />
              </button>
              {showExportMenu && (
                <div className="export-menu-list" onMouseLeave={() => setShowExportMenu(false)}>
                  {view === "charts" && (
                    <button
                      disabled={exporting}
                      onClick={() => {
                        setShowExportMenu(false);
                        void exportCsv();
                      }}
                    >
                      {exporting ? "Exporting CSV…" : "CSV (current view)"}
                    </button>
                  )}
                  <button
                    disabled={exportingPng}
                    onClick={() => {
                      setShowExportMenu(false);
                      void exportPng();
                    }}
                  >
                    {exportingPng ? "Exporting PNG…" : "PNG (chart image)"}
                  </button>
                </div>
              )}
            </div>
          </div>

          {view === "charts" && compare && (
            <div className="compare-row">
              <span className="compare-chip">
                <Icon name="compare" />
                vs <b className="compare-name" title={basename(compare.sourcePath)}>{basename(compare.sourcePath)}</b>
                {compareTune ? ` (${compareTune})` : ""}
                <label className="offset-label">
                  offset
                  <input
                    type="number"
                    step="0.5"
                    value={compareOffset}
                    onChange={(e) => setCompareOffset(Number(e.target.value) || 0)}
                  />
                  s
                </label>
                <button className="chip-x" title="Close comparison" onClick={closeCompare}>
                  ×
                </button>
              </span>
            </div>
          )}
        </header>
      )}

      <div className="body">
        <aside className="sidebar">
          {log ? (
            <>
              <input
                className="channel-filter"
                placeholder="Filter channels…"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              />
              <MathPanel
                presets={presets}
                customChannels={customMathChannels}
                inPane={
                  new Set(
                    activePane.channelIds
                      .map((id) => channelById.get(id))
                      .filter((c): c is ChannelDto => !!c && c.computed)
                      .map((c) => c.name),
                  )
                }
                error={mathError}
                showCustom={showCustomMath}
                onToggleCustom={() => setShowCustomMath((v) => !v)}
                onTogglePreset={(p) => {
                  const channel = log.channels.find((c) => c.computed && c.name === p.name);
                  if (channel) toggleChannel(channel.id);
                }}
                onAddCustom={(name, unit, expression) => void addMathChannel(name, unit, expression)}
                onDeleteCustom={(name) => void deleteCustomMathChannel(name)}
              />
              <div className="sidebar-hint">
                Click to add/remove from <b>Pane {activePane.id}</b> — or drag onto any pane
              </div>
              <div className="channel-list">
                {groupedChannels.map((g) => {
                  const collapsed = collapsedGroups.has(g.key);
                  return (
                    <div className="channel-group" key={g.key}>
                      <button className="channel-group-head" onClick={() => toggleGroupCollapsed(g.key)}>
                        <Icon name={g.icon} className="channel-group-icon" />
                        <span className="channel-group-label">{g.label}</span>
                        <span className="channel-group-count">{g.channels.length}</span>
                        <Icon name="chevron" className={`channel-group-chevron${collapsed ? " collapsed" : ""}`} />
                      </button>
                      {!collapsed && (
                        <ul className="channel-sublist">
                          {g.channels.map((c) => {
                            const inActive = activePane.channelIds.includes(c.id);
                            const elsewhere = !inActive && panes.some((p) => p.channelIds.includes(c.id));
                            return (
                              <li key={c.id}>
                                <button
                                  className={`channel${inActive ? " selected" : ""}${elsewhere ? " elsewhere" : ""}`}
                                  draggable
                                  onDragStart={(e) => {
                                    e.dataTransfer.setData(CHANNEL_DRAG_TYPE, String(c.id));
                                    e.dataTransfer.effectAllowed = "copy";
                                  }}
                                  onClick={() => toggleChannel(c.id)}
                                >
                                  {c.computed && <span className="fx">ƒ </span>}
                                  {c.name}
                                  {c.unit && <span className="unit"> ({displayUnit(c.unit, unitPrefs)})</span>}
                                </button>
                              </li>
                            );
                          })}
                        </ul>
                      )}
                    </div>
                  );
                })}
              </div>
            </>
          ) : (
            <p className="hint">Open an ECU Connect CSV log to list channels.</p>
          )}
        </aside>

        <main className="chart-column" ref={chartColumnRef}>
          {log && (view === "histogram" || view === "scatter") ? (
            <div className="filterable-view">
              <FilterBar channels={log.channels} filters={filters} onChange={setFilters} />
              {view === "histogram" ? (
                <HistogramView
                  key={log.logId}
                  channels={log.channels}
                  filters={filters}
                  fetchHistogram={fetchHistogram}
                  unitPrefs={unitPrefs}
                  resolvedDark={resolvedDark}
                />
              ) : range ? (
                <ScatterView
                  key={log.logId}
                  channels={log.channels}
                  range={range}
                  fullRange={log.timeRange}
                  filters={filters}
                  fetchScatter={fetchScatter}
                  unitPrefs={unitPrefs}
                  resolvedDark={resolvedDark}
                />
              ) : null}
            </div>
          ) : log && view === "replay" ? (
            <ReplayView
              key={log.logId}
              channels={log.channels}
              fullRange={log.timeRange}
              fetchRawSeries={fetchRawSeries}
              unitPrefs={unitPrefs}
            />
          ) : log && range ? (
            <>
              {panes.map((pane, paneIndex) => (
                <Fragment key={pane.id}>
                  {paneIndex > 0 && (
                    <div
                      key={`resize-${pane.id}`}
                      className="pane-resize-handle"
                      onMouseDown={(e) => startPaneResize(e, panes[paneIndex - 1].id, pane.id)}
                    />
                  )}
                  <section
                    key={pane.id}
                    data-pane-id={pane.id}
                    className={`pane${pane.id === activePane.id ? " active" : ""}${dragOverPaneId === pane.id ? " drag-target" : ""}`}
                    style={{ flexGrow: paneGrow[pane.id] ?? 1 }}
                    onClick={() => setActivePaneId(pane.id)}
                    onDragOver={(e) => {
                      if (!e.dataTransfer.types.includes(CHANNEL_DRAG_TYPE)) return;
                      e.preventDefault();
                      e.stopPropagation();
                      e.dataTransfer.dropEffect = "copy";
                      setDragOverPaneId(pane.id);
                    }}
                    onDragLeave={(e) => {
                      if (e.currentTarget.contains(e.relatedTarget as Node | null)) return;
                      setDragOverPaneId((id) => (id === pane.id ? null : id));
                    }}
                    onDrop={(e) => {
                      const raw = e.dataTransfer.getData(CHANNEL_DRAG_TYPE);
                      if (!raw) return;
                      e.preventDefault();
                      e.stopPropagation();
                      setDragOverPaneId(null);
                      setIsDragOver(false);
                      addChannelToPane(pane.id, Number(raw));
                    }}
                  >
                  <div className="pane-header">
                    <span className="pane-title">Pane {pane.id}</span>
                    {pane.channelIds.map((channelId, i) => {
                      const c = channelById.get(channelId);
                      const conditions = thresholds.get(channelId) ?? [];
                      const rawLabel = c ? (c.unit ? `${c.name} (${c.unit})` : c.name) : `#${channelId}`;
                      const customColor = channelColors[rawLabel];
                      const chipColor = customColor ?? palette[i % palette.length];
                      return (
                        <span key={channelId} className="chip" style={{ borderColor: chipColor }}>
                          <label
                            className="chip-swatch"
                            style={{ backgroundColor: chipColor }}
                            title="Pick a custom color for this channel"
                            onClick={(e) => e.stopPropagation()}
                          >
                            <input
                              type="color"
                              value={chipColor}
                              onChange={(e) => void setChannelColor(rawLabel, e.target.value)}
                            />
                          </label>
                          {c?.name ?? `#${channelId}`}
                          {customColor && (
                            <button
                              className="chip-color-reset"
                              title="Reset to default color"
                              onClick={(e) => {
                                e.stopPropagation();
                                void clearChannelColor(rawLabel);
                              }}
                            >
                              ↺
                            </button>
                          )}
                          <button
                            className={`chip-alarm${conditions.length > 0 ? " active" : ""}`}
                            title={
                              conditions.length > 0
                                ? `Alarm: ${conditions.map((c) => `${c.op === "gt" ? ">" : "<"} ${c.value}`).join(" or ")}`
                                : "Set alarm threshold"
                            }
                            onClick={(e) => {
                              e.stopPropagation();
                              setThresholdDraft((prev) =>
                                prev?.channelId === channelId ? null : { channelId, op: "gt", value: "" },
                              );
                            }}
                          >
                            <Icon name="warn" />
                          </button>
                          <button
                            className="chip-x"
                            onClick={(e) => {
                              e.stopPropagation();
                              removeFromPane(pane.id, channelId);
                            }}
                          >
                            ×
                          </button>
                          {thresholdDraft?.channelId === channelId && (
                            <span className="threshold-editor" onClick={(e) => e.stopPropagation()}>
                              {conditions.length > 0 && (
                                <ul className="threshold-list">
                                  {conditions.map((cond, idx) => (
                                    <li key={idx}>
                                      {cond.op === "gt" ? ">" : "<"} {cond.value}
                                      <button onClick={() => removeThresholdCondition(channelId, idx)}>×</button>
                                    </li>
                                  ))}
                                </ul>
                              )}
                              <span className="threshold-add-row">
                                <select
                                  value={thresholdDraft.op}
                                  onChange={(e) =>
                                    setThresholdDraft({
                                      ...thresholdDraft,
                                      op: e.target.value as "gt" | "lt",
                                    })
                                  }
                                >
                                  <option value="gt">&gt;</option>
                                  <option value="lt">&lt;</option>
                                </select>
                                <input
                                  type="number"
                                  autoFocus
                                  value={thresholdDraft.value}
                                  onChange={(e) =>
                                    setThresholdDraft({ ...thresholdDraft, value: e.target.value })
                                  }
                                  onKeyDown={(e) => {
                                    if (e.key === "Enter") addThresholdCondition();
                                    if (e.key === "Escape") setThresholdDraft(null);
                                  }}
                                />
                                <button onClick={addThresholdCondition}>+ Add</button>
                              </span>
                            </span>
                          )}
                        </span>
                      );
                    })}
                    <button
                      className={`pane-lock${pane.yLocked ? " active" : ""}`}
                      title={pane.yLocked ? "Unlock y-axis" : "Lock y-axis range"}
                      onClick={(e) => {
                        e.stopPropagation();
                        setPanes((prev) =>
                          prev.map((p) => (p.id === pane.id ? { ...p, yLocked: !p.yLocked } : p)),
                        );
                      }}
                    >
                      <Icon name="lock" />
                    </button>
                    {panes.length > 1 && (
                      <button
                        className="pane-x"
                        title="Remove pane"
                        onClick={(e) => {
                          e.stopPropagation();
                          removePane(pane.id);
                        }}
                      >
                        ×
                      </button>
                    )}
                  </div>
                  {pane.channelIds.length > 0 ? (
                    <Chart
                      series={paneSeries(pane).specs}
                      data={dataByPane[pane.id] ?? null}
                      range={range}
                      fullRange={log.timeRange}
                      onRangeChange={setRange}
                      onWidthChange={setWidth}
                      yLocked={pane.yLocked}
                      onSelectionStats={(r) => void handleSelectionStats(r)}
                      marks={marks}
                      annotations={annotations}
                      onAddAnnotation={pane.id === panes[0]?.id ? (t) => void addAnnotationAt(t) : undefined}
                      unitPrefs={unitPrefs}
                      resolvedDark={resolvedDark}
                    />
                  ) : (
                    <div className="empty pane-empty">
                      <p className="hint">Click channels in the sidebar to plot them here.</p>
                    </div>
                  )}
                  </section>
                </Fragment>
              ))}
              <button className="add-pane" onClick={addPane}>
                + Pane
              </button>
              {selectionStats && (
                <SelectionStatsPanel
                  range={selectionStats.range}
                  stats={selectionStats.stats}
                  channels={log.channels}
                  onClose={() => setSelectionStats(null)}
                  unitPrefs={unitPrefs}
                />
              )}
              <AnnotationsPanel annotations={annotations} onDelete={(id) => void deleteAnnotation(id)} />
            </>
          ) : (
            <div className="welcome">
              <div className="welcome-mark">📈</div>
              <h1>No log open</h1>
              <p className="welcome-sub">Open an ECU Connect CSV export to start plotting.</p>
              <button onClick={() => void openLog()} disabled={status.kind !== "ready"}>
                Open Log…
              </button>
              {recentLogs.length > 0 && (
                <div className="welcome-recent">
                  <h2>Recent</h2>
                  <ul>
                    {recentLogs.map((entry) => (
                      <li key={entry.path}>
                        <button
                          className="welcome-recent-item"
                          disabled={!entry.exists}
                          title={entry.path}
                          onClick={() => void openNewTab(entry.path)}
                        >
                          {basename(entry.path)}
                          {!entry.exists && <span className="recent-menu-missing"> (missing)</span>}
                        </button>
                        <button
                          className="chip-x"
                          title="Remove from recent logs"
                          onClick={() => void removeRecentLog(entry.path)}
                        >
                          ×
                        </button>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              <ul className="welcome-hints">
                <li>Drag-select or scroll to zoom</li>
                <li>Shift+drag for selection stats</li>
                <li>Ctrl/Cmd+click to add a note</li>
                <li>Horizontal scroll to pan</li>
                <li>Double-click to reset the view</li>
              </ul>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}

function defaultPanes(channels: ChannelDto[]): Pane[] {
  const find = (prefix: string) => channels.find((c) => c.name.startsWith(prefix))?.id;
  const boostActual = find("Boost Pressure Actual");
  const boostTarget = find("Boost Pressure Target");
  const boostError = find("Boost Error");

  const pane1 = [boostActual, boostTarget].filter((id): id is number => id !== undefined);
  const pane2 = [boostError].filter((id): id is number => id !== undefined);

  if (pane1.length === 0) return [{ id: 1, channelIds: channels.length > 0 ? [channels[0].id] : [] }];
  return pane2.length > 0
    ? [
        { id: 1, channelIds: pane1 },
        { id: 2, channelIds: pane2 },
      ]
    : [{ id: 1, channelIds: pane1 }];
}

function basename(p: string): string {
  return p.split(/[\\/]/).pop() ?? p;
}
