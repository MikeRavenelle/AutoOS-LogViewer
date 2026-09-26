namespace OpenLogViewer.Engine.Host.Protocol;

public sealed record EmptyResult;

public static class Commands
{
    public static readonly (string Name, Type Request, Type Result)[] All =
    [
        ("openLog", typeof(OpenLogRequest), typeof(OpenLogResult)),
        ("getSeries", typeof(GetSeriesRequest), typeof(GetSeriesResult)),
        ("closeLog", typeof(CloseLogRequest), typeof(EmptyResult)),
        ("listMathPresets", typeof(ListMathPresetsRequest), typeof(ListMathPresetsResult)),
        ("addMathChannel", typeof(AddMathChannelRequest), typeof(AddMathChannelResult)),
        ("getOverlaySeries", typeof(GetOverlayRequest), typeof(GetOverlayResult)),
        ("getHistogram", typeof(GetHistogramRequest), typeof(GetHistogramResult)),
        ("getScatter", typeof(GetScatterRequest), typeof(GetScatterResult)),
        ("getSelectionStats", typeof(GetSelectionStatsRequest), typeof(GetSelectionStatsResult)),
        ("exportRange", typeof(ExportRangeRequest), typeof(ExportRangeResult)),
        ("listMarks", typeof(ListMarksRequest), typeof(ListMarksResult)),
        ("getRawSeries", typeof(GetRawSeriesRequest), typeof(GetRawSeriesResult)),
        ("listAnnotations", typeof(ListAnnotationsRequest), typeof(AnnotationsResult)),
        ("addAnnotation", typeof(AddAnnotationRequest), typeof(AnnotationsResult)),
        ("deleteAnnotation", typeof(DeleteAnnotationRequest), typeof(AnnotationsResult)),
        ("listLayouts", typeof(ListLayoutsRequest), typeof(ListLayoutsResult)),
        ("saveLayout", typeof(SaveLayoutRequest), typeof(ListLayoutsResult)),
        ("deleteLayout", typeof(DeleteLayoutRequest), typeof(ListLayoutsResult)),
        ("listChannelColors", typeof(ListChannelColorsRequest), typeof(ChannelColorsResult)),
        ("setChannelColor", typeof(SetChannelColorRequest), typeof(ChannelColorsResult)),
        ("clearChannelColor", typeof(ClearChannelColorRequest), typeof(ChannelColorsResult)),
        ("listRecentLogs", typeof(ListRecentLogsRequest), typeof(RecentLogsResult)),
        ("removeRecentLog", typeof(RemoveRecentLogRequest), typeof(RecentLogsResult)),
        ("listCustomMathChannels", typeof(ListCustomMathChannelsRequest), typeof(CustomMathChannelsResult)),
        ("saveCustomMathChannel", typeof(SaveCustomMathChannelRequest), typeof(CustomMathChannelsResult)),
        ("deleteCustomMathChannel", typeof(DeleteCustomMathChannelRequest), typeof(CustomMathChannelsResult)),
    ];
}

public sealed record ParserDto(string Id, string DisplayName);

public sealed record HelloPayload(int ProtocolVersion, string EngineVersion, ParserDto[] Parsers);

public sealed record ErrorDto(string Code, string Message);

public sealed record ChannelDto(int Id, string Name, string Unit, bool Computed);

public sealed record ListMathPresetsRequest(int LogId);

public sealed record MathPresetDto(string Id, string Name, string Unit, string Expression, bool Available);

public sealed record ListMathPresetsResult(MathPresetDto[] Presets);

public sealed record AddMathChannelRequest(int LogId, string Name, string Unit, string Expression);

public sealed record AddMathChannelResult(int LogId, ChannelDto Channel);

public sealed record TimeRangeDto(double Start, double End);

public sealed record OpenLogRequest(string Path);

public sealed record OpenLogResult(
    int LogId,
    string SourcePath,
    string ParserId,
    IReadOnlyDictionary<string, string> Metadata,
    int SampleCount,
    TimeRangeDto TimeRange,
    ChannelDto[] Channels);

public sealed record GetSeriesRequest(int LogId, int[] ChannelIds, double T0, double T1, int PixelWidth);

public sealed record AlignedSeriesDto(int ChannelId, float?[] V);

public sealed record GetSeriesResult(int LogId, double T0, double T1, double[] T, AlignedSeriesDto[] Series);

public sealed record CloseLogRequest(int LogId);

public sealed record OverlaySourceDto(int LogId, int ChannelId, double Offset);

public sealed record GetOverlayRequest(OverlaySourceDto[] Sources, double T0, double T1, int PixelWidth);

public sealed record OverlaySeriesDto(int LogId, int ChannelId, float?[] V);

public sealed record GetOverlayResult(double T0, double T1, double[] T, OverlaySeriesDto[] Series);

public sealed record FilterDto(int ChannelId, string Op, double Value);

public sealed record GetHistogramRequest(
    int LogId, int XChannelId, int YChannelId, int ValueChannelId,
    int XBins, int YBins, string Agg, int MinCount, FilterDto[]? Filters = null);

public sealed record HistogramDto(double[] XEdges, double[] YEdges, double?[][] Cells, int[][] Counts);

public sealed record GetHistogramResult(int LogId, HistogramDto Histogram);

public sealed record GetScatterRequest(
    int LogId, int XChannelId, int YChannelId, int? ColorChannelId,
    double T0, double T1, int MaxPoints, FilterDto[]? Filters = null);

public sealed record ScatterDto(float[] X, float[] Y, float?[] V, int TotalSamples);

public sealed record GetScatterResult(int LogId, ScatterDto Scatter);

public sealed record GetSelectionStatsRequest(int LogId, int[] ChannelIds, double T0, double T1, FilterDto[]? Filters = null);

public sealed record ChannelStatsDto(
    int ChannelId, double? Min, double? Max, double? Avg, double? Delta,
    double? StdDev, double? Variance, double? Median, double? P95,
    int Count);

public sealed record GetSelectionStatsResult(int LogId, ChannelStatsDto[] Stats);

public sealed record ExportRangeRequest(int LogId, int[] ChannelIds, double T0, double T1);

public sealed record ExportRangeResult(string Csv);

public sealed record ListMarksRequest(int LogId);

public sealed record ListMarksResult(double[] Times);

public sealed record GetRawSeriesRequest(int LogId, int[] ChannelIds, double T0, double T1);

public sealed record RawSeriesDto(int ChannelId, float[] V);

public sealed record GetRawSeriesResult(int LogId, double[] T, RawSeriesDto[] Series);

public sealed record AnnotationDto(string Id, double Time, string Text);

public sealed record ListAnnotationsRequest(int LogId);

public sealed record AddAnnotationRequest(int LogId, double Time, string Text);

public sealed record DeleteAnnotationRequest(int LogId, string Id);

public sealed record AnnotationsResult(AnnotationDto[] Annotations);

public sealed record LayoutPaneDto(string[] ChannelLabels);

public sealed record LayoutDto(string Name, LayoutPaneDto[] Panes);

public sealed record ListLayoutsRequest;

public sealed record ListLayoutsResult(LayoutDto[] Layouts);

public sealed record SaveLayoutRequest(string Name, LayoutPaneDto[] Panes);

public sealed record DeleteLayoutRequest(string Name);

public sealed record ListChannelColorsRequest;

public sealed record ChannelColorsResult(Dictionary<string, string> Colors);

public sealed record SetChannelColorRequest(string Label, string Color);

public sealed record ClearChannelColorRequest(string Label);

public sealed record RecentLogEntryDto(string Path, string OpenedAt, bool Exists);

public sealed record ListRecentLogsRequest;

public sealed record RecentLogsResult(RecentLogEntryDto[] Logs);

public sealed record RemoveRecentLogRequest(string Path);

public sealed record CustomMathChannelDto(string Name, string Unit, string Expression);

public sealed record ListCustomMathChannelsRequest;

public sealed record CustomMathChannelsResult(CustomMathChannelDto[] Channels);

public sealed record SaveCustomMathChannelRequest(string Name, string Unit, string Expression);

public sealed record DeleteCustomMathChannelRequest(string Name);

public static class ErrorCodes
{
    public const string BadRequest = "badRequest";
    public const string UnknownCommand = "unknownCommand";
    public const string FileNotFound = "fileNotFound";
    public const string NoParser = "noParser";
    public const string ParseError = "parseError";
    public const string UnknownLog = "unknownLog";
    public const string UnknownChannel = "unknownChannel";
    public const string ExpressionError = "expressionError";
    public const string Internal = "internal";
}
