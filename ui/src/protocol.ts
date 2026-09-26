export interface OpenLogRequest {
  path: string;
}

export interface TimeRangeDto {
  start: number;
  end: number;
}

export interface ChannelDto {
  id: number;
  name: string;
  unit: string;
  computed: boolean;
}

export interface OpenLogResult {
  logId: number;
  sourcePath: string;
  parserId: string;
  metadata: Record<string, string>;
  sampleCount: number;
  timeRange: TimeRangeDto;
  channels: ChannelDto[];
}

export interface GetSeriesRequest {
  logId: number;
  channelIds: number[];
  t0: number;
  t1: number;
  pixelWidth: number;
}

export interface AlignedSeriesDto {
  channelId: number;
  v: (number | null)[];
}

export interface GetSeriesResult {
  logId: number;
  t0: number;
  t1: number;
  t: number[];
  series: AlignedSeriesDto[];
}

export interface CloseLogRequest {
  logId: number;
}

export interface ListMathPresetsRequest {
  logId: number;
}

export interface MathPresetDto {
  id: string;
  name: string;
  unit: string;
  expression: string;
  available: boolean;
}

export interface ListMathPresetsResult {
  presets: MathPresetDto[];
}

export interface AddMathChannelRequest {
  logId: number;
  name: string;
  unit: string;
  expression: string;
}

export interface AddMathChannelResult {
  logId: number;
  channel: ChannelDto;
}

export interface OverlaySourceDto {
  logId: number;
  channelId: number;
  offset: number;
}

export interface GetOverlayRequest {
  sources: OverlaySourceDto[];
  t0: number;
  t1: number;
  pixelWidth: number;
}

export interface OverlaySeriesDto {
  logId: number;
  channelId: number;
  v: (number | null)[];
}

export interface GetOverlayResult {
  t0: number;
  t1: number;
  t: number[];
  series: OverlaySeriesDto[];
}

export interface FilterDto {
  channelId: number;
  op: string;
  value: number;
}

export interface GetHistogramRequest {
  logId: number;
  xChannelId: number;
  yChannelId: number;
  valueChannelId: number;
  xBins: number;
  yBins: number;
  agg: string;
  minCount: number;
  filters: FilterDto[];
}

export interface HistogramDto {
  xEdges: number[];
  yEdges: number[];
  cells: (number | null)[][];
  counts: number[][];
}

export interface GetHistogramResult {
  logId: number;
  histogram: HistogramDto;
}

export interface GetScatterRequest {
  logId: number;
  xChannelId: number;
  yChannelId: number;
  colorChannelId: (number | null);
  t0: number;
  t1: number;
  maxPoints: number;
  filters: FilterDto[];
}

export interface ScatterDto {
  x: number[];
  y: number[];
  v: (number | null)[];
  totalSamples: number;
}

export interface GetScatterResult {
  logId: number;
  scatter: ScatterDto;
}

export interface GetSelectionStatsRequest {
  logId: number;
  channelIds: number[];
  t0: number;
  t1: number;
  filters: FilterDto[];
}

export interface ChannelStatsDto {
  channelId: number;
  min: (number | null);
  max: (number | null);
  avg: (number | null);
  delta: (number | null);
  stdDev: (number | null);
  variance: (number | null);
  median: (number | null);
  p95: (number | null);
  count: number;
}

export interface GetSelectionStatsResult {
  logId: number;
  stats: ChannelStatsDto[];
}

export interface ExportRangeRequest {
  logId: number;
  channelIds: number[];
  t0: number;
  t1: number;
}

export interface ExportRangeResult {
  csv: string;
}

export interface ListMarksRequest {
  logId: number;
}

export interface ListMarksResult {
  times: number[];
}

export interface GetRawSeriesRequest {
  logId: number;
  channelIds: number[];
  t0: number;
  t1: number;
}

export interface RawSeriesDto {
  channelId: number;
  v: number[];
}

export interface GetRawSeriesResult {
  logId: number;
  t: number[];
  series: RawSeriesDto[];
}

export interface ListAnnotationsRequest {
  logId: number;
}

export interface AnnotationDto {
  id: string;
  time: number;
  text: string;
}

export interface AnnotationsResult {
  annotations: AnnotationDto[];
}

export interface AddAnnotationRequest {
  logId: number;
  time: number;
  text: string;
}

export interface DeleteAnnotationRequest {
  logId: number;
  id: string;
}

export interface ListLayoutsRequest {
}

export interface LayoutPaneDto {
  channelLabels: string[];
}

export interface LayoutDto {
  name: string;
  panes: LayoutPaneDto[];
}

export interface ListLayoutsResult {
  layouts: LayoutDto[];
}

export interface SaveLayoutRequest {
  name: string;
  panes: LayoutPaneDto[];
}

export interface DeleteLayoutRequest {
  name: string;
}

export interface ListChannelColorsRequest {
}

export interface ChannelColorsResult {
  colors: Record<string, string>;
}

export interface SetChannelColorRequest {
  label: string;
  color: string;
}

export interface ClearChannelColorRequest {
  label: string;
}

export interface ListRecentLogsRequest {
}

export interface RecentLogEntryDto {
  path: string;
  openedAt: string;
  exists: boolean;
}

export interface RecentLogsResult {
  logs: RecentLogEntryDto[];
}

export interface RemoveRecentLogRequest {
  path: string;
}

export interface ListCustomMathChannelsRequest {
}

export interface CustomMathChannelDto {
  name: string;
  unit: string;
  expression: string;
}

export interface CustomMathChannelsResult {
  channels: CustomMathChannelDto[];
}

export interface SaveCustomMathChannelRequest {
  name: string;
  unit: string;
  expression: string;
}

export interface DeleteCustomMathChannelRequest {
  name: string;
}

export interface ParserDto {
  id: string;
  displayName: string;
}

export interface HelloPayload {
  protocolVersion: number;
  engineVersion: string;
  parsers: ParserDto[];
}

export interface ErrorDto {
  code: string;
  message: string;
}

export interface RequestPayloads {
  openLog: OpenLogRequest;
  getSeries: GetSeriesRequest;
  closeLog: CloseLogRequest;
  listMathPresets: ListMathPresetsRequest;
  addMathChannel: AddMathChannelRequest;
  getOverlaySeries: GetOverlayRequest;
  getHistogram: GetHistogramRequest;
  getScatter: GetScatterRequest;
  getSelectionStats: GetSelectionStatsRequest;
  exportRange: ExportRangeRequest;
  listMarks: ListMarksRequest;
  getRawSeries: GetRawSeriesRequest;
  listAnnotations: ListAnnotationsRequest;
  addAnnotation: AddAnnotationRequest;
  deleteAnnotation: DeleteAnnotationRequest;
  listLayouts: ListLayoutsRequest;
  saveLayout: SaveLayoutRequest;
  deleteLayout: DeleteLayoutRequest;
  listChannelColors: ListChannelColorsRequest;
  setChannelColor: SetChannelColorRequest;
  clearChannelColor: ClearChannelColorRequest;
  listRecentLogs: ListRecentLogsRequest;
  removeRecentLog: RemoveRecentLogRequest;
  listCustomMathChannels: ListCustomMathChannelsRequest;
  saveCustomMathChannel: SaveCustomMathChannelRequest;
  deleteCustomMathChannel: DeleteCustomMathChannelRequest;
}

export interface ResultTypes {
  openLog: OpenLogResult;
  getSeries: GetSeriesResult;
  closeLog: Record<string, never>;
  listMathPresets: ListMathPresetsResult;
  addMathChannel: AddMathChannelResult;
  getOverlaySeries: GetOverlayResult;
  getHistogram: GetHistogramResult;
  getScatter: GetScatterResult;
  getSelectionStats: GetSelectionStatsResult;
  exportRange: ExportRangeResult;
  listMarks: ListMarksResult;
  getRawSeries: GetRawSeriesResult;
  listAnnotations: AnnotationsResult;
  addAnnotation: AnnotationsResult;
  deleteAnnotation: AnnotationsResult;
  listLayouts: ListLayoutsResult;
  saveLayout: ListLayoutsResult;
  deleteLayout: ListLayoutsResult;
  listChannelColors: ChannelColorsResult;
  setChannelColor: ChannelColorsResult;
  clearChannelColor: ChannelColorsResult;
  listRecentLogs: RecentLogsResult;
  removeRecentLog: RecentLogsResult;
  listCustomMathChannels: CustomMathChannelsResult;
  saveCustomMathChannel: CustomMathChannelsResult;
  deleteCustomMathChannel: CustomMathChannelsResult;
}

export type Command = keyof RequestPayloads;
