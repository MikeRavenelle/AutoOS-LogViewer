using System.Text.Json;
using System.Text.Json.Nodes;
using OpenLogViewer.Engine.Expressions;
using OpenLogViewer.Engine.Host.Protocol;
using OpenLogViewer.Engine.Parsing;
using OpenLogViewer.Engine.Series;

namespace OpenLogViewer.Engine.Host;

public sealed class CommandDispatcher(ParserRegistry parsers, LogStore store, LayoutStore layouts, AnnotationStore annotations, ChannelColorStore channelColors, RecentLogsStore recentLogs, CustomMathChannelStore customMathChannels)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public string HelloEvent()
    {
        var payload = new HelloPayload(
            ProtocolVersion: 1,
            EngineVersion: typeof(CommandDispatcher).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            Parsers: [.. parsers.Parsers.Select(p => new ParserDto(p.Id, p.DisplayName))]);
        return JsonSerializer.Serialize(new { @event = "hello", payload }, JsonOptions);
    }

    public string Handle(string requestJson)
    {
        long id = -1;
        try
        {
            var node = JsonNode.Parse(requestJson)?.AsObject()
                ?? throw new ProtocolException(ErrorCodes.BadRequest, "Frame is not a JSON object.");
            id = node["id"]?.GetValue<long>()
                ?? throw new ProtocolException(ErrorCodes.BadRequest, "Missing 'id'.");
            string cmd = node["cmd"]?.GetValue<string>()
                ?? throw new ProtocolException(ErrorCodes.BadRequest, "Missing 'cmd'.");
            var payload = node["payload"];

            object result = cmd switch
            {
                "openLog" => OpenLog(Deserialize<OpenLogRequest>(payload)),
                "getSeries" => GetSeries(Deserialize<GetSeriesRequest>(payload)),
                "closeLog" => CloseLog(Deserialize<CloseLogRequest>(payload)),
                "getOverlaySeries" => GetOverlaySeries(Deserialize<GetOverlayRequest>(payload)),
                "getHistogram" => GetHistogram(Deserialize<GetHistogramRequest>(payload)),
                "getScatter" => GetScatter(Deserialize<GetScatterRequest>(payload)),
                "getSelectionStats" => GetSelectionStats(Deserialize<GetSelectionStatsRequest>(payload)),
                "exportRange" => ExportRange(Deserialize<ExportRangeRequest>(payload)),
                "listMarks" => ListMarks(Deserialize<ListMarksRequest>(payload)),
                "getRawSeries" => GetRawSeries(Deserialize<GetRawSeriesRequest>(payload)),
                "listAnnotations" => ListAnnotations(Deserialize<ListAnnotationsRequest>(payload)),
                "addAnnotation" => AddAnnotation(Deserialize<AddAnnotationRequest>(payload)),
                "deleteAnnotation" => DeleteAnnotation(Deserialize<DeleteAnnotationRequest>(payload)),
                "listMathPresets" => ListMathPresets(Deserialize<ListMathPresetsRequest>(payload)),
                "addMathChannel" => AddMathChannel(Deserialize<AddMathChannelRequest>(payload)),
                "listLayouts" => new ListLayoutsResult(layouts.List()),
                "saveLayout" => SaveLayout(Deserialize<SaveLayoutRequest>(payload)),
                "deleteLayout" => new ListLayoutsResult(layouts.Delete(Deserialize<DeleteLayoutRequest>(payload).Name)),
                "listChannelColors" => new ChannelColorsResult(channelColors.List()),
                "setChannelColor" => SetChannelColor(Deserialize<SetChannelColorRequest>(payload)),
                "clearChannelColor" => new ChannelColorsResult(channelColors.Clear(Deserialize<ClearChannelColorRequest>(payload).Label)),
                "listRecentLogs" => new RecentLogsResult(ToRecentLogDtos(recentLogs.List())),
                "removeRecentLog" => new RecentLogsResult(ToRecentLogDtos(recentLogs.Remove(Deserialize<RemoveRecentLogRequest>(payload).Path))),
                "listCustomMathChannels" => new CustomMathChannelsResult(ToCustomMathChannelDtos(customMathChannels.List())),
                "saveCustomMathChannel" => SaveCustomMathChannel(Deserialize<SaveCustomMathChannelRequest>(payload)),
                "deleteCustomMathChannel" => new CustomMathChannelsResult(ToCustomMathChannelDtos(customMathChannels.Delete(Deserialize<DeleteCustomMathChannelRequest>(payload).Name))),
                _ => throw new ProtocolException(ErrorCodes.UnknownCommand, $"Unknown command '{cmd}'."),
            };
            return JsonSerializer.Serialize(new { id, ok = true, result }, JsonOptions);
        }
        catch (ProtocolException ex)
        {
            return Error(id, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return Error(id, ErrorCodes.Internal, ex.Message);
        }
    }

    private static T Deserialize<T>(JsonNode? payload)
        => (payload ?? new JsonObject()).Deserialize<T>(JsonOptions)
           ?? throw new ProtocolException(ErrorCodes.BadRequest, $"Missing or invalid payload for {typeof(T).Name}.");

    private OpenLogResult OpenLog(OpenLogRequest req)
    {
        if (!File.Exists(req.Path))
            throw new ProtocolException(ErrorCodes.FileNotFound, $"File not found: {req.Path}");

        var parser = parsers.ResolveFor(req.Path)
            ?? throw new ProtocolException(ErrorCodes.NoParser, "No registered parser recognizes this file.");

        Model.ParsedLog log;
        try
        {
            log = parser.Parse(req.Path);
        }
        catch (Exception ex) when (ex is not ProtocolException)
        {
            throw new ProtocolException(ErrorCodes.ParseError, ex.Message);
        }

        foreach (var preset in MathPresets.All)
        {
            try
            {
                if (!MathPresets.IsAvailable(preset, log))
                    continue;
                var expr = ExpressionParser.Parse(preset.Expression);
                log.AddComputedChannel(preset.Name, preset.Unit, Evaluator.Evaluate(expr, log));
            }
            catch (Exception ex) when (ex is ExpressionParseException or ExpressionEvalException)
            {
            }
        }

        foreach (var custom in customMathChannels.List())
        {
            try
            {
                var expr = ExpressionParser.Parse(custom.Expression);
                foreach (var reference in ExpressionParser.ChannelReferences(expr))
                {
                    if (Evaluator.Resolve(log, reference) is null)
                        throw new ExpressionEvalException($"Unknown channel [{reference}]");
                }
                log.AddComputedChannel(custom.Name, custom.Unit, Evaluator.Evaluate(expr, log));
            }
            catch (Exception ex) when (ex is ExpressionParseException or ExpressionEvalException)
            {
            }
        }

        var (logId, _) = store.Add(log);
        recentLogs.Touch(req.Path);
        return new OpenLogResult(
            logId,
            log.SourcePath,
            log.ParserId,
            log.Metadata,
            log.SampleCount,
            new TimeRangeDto(log.StartTime, log.EndTime),
            [.. log.Channels.Select(ToDto)]);
    }

    private static ChannelDto ToDto(Model.ChannelInfo c) => new(c.Id, c.Name, c.Unit, c.Computed);

    private GetOverlayResult GetOverlaySeries(GetOverlayRequest req)
    {
        var sources = req.Sources;
        var logs = new Model.ParsedLog[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            logs[i] = store.Get(sources[i].LogId)
                ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {sources[i].LogId}.");
            if (sources[i].ChannelId < 0 || sources[i].ChannelId >= logs[i].Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel,
                    $"No channel with id {sources[i].ChannelId} in log {sources[i].LogId}.");
        }

        var decimated = new DecimatedSeries[sources.Length];
        Parallel.For(0, sources.Length, i =>
        {
            var src = sources[i];
            var slice = Decimator.Decimate(
                logs[i].Time, logs[i].Data[src.ChannelId],
                req.T0 - src.Offset, req.T1 - src.Offset, req.PixelWidth);
            if (src.Offset != 0)
            {
                var shifted = new double[slice.T.Length];
                for (int k = 0; k < shifted.Length; k++) shifted[k] = slice.T[k] + src.Offset;
                slice = slice with { T = shifted };
            }
            decimated[i] = slice;
        });

        var (t, columns) = SeriesAligner.Align(decimated);
        var series = new OverlaySeriesDto[sources.Length];
        for (int i = 0; i < series.Length; i++)
            series[i] = new OverlaySeriesDto(sources[i].LogId, sources[i].ChannelId, columns[i]);
        return new GetOverlayResult(req.T0, req.T1, t, series);
    }

    private GetHistogramResult GetHistogram(GetHistogramRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        foreach (int channelId in (int[])[req.XChannelId, req.YChannelId, req.ValueChannelId])
        {
            if (channelId < 0 || channelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {channelId}.");
        }
        if (!Enum.TryParse<HistogramAgg>(req.Agg, ignoreCase: true, out var agg))
            throw new ProtocolException(ErrorCodes.BadRequest, $"Unknown agg '{req.Agg}' (mean|min|max|count).");

        var filters = BuildFilters(req.Filters, log);
        var grid = Histogrammer.Compute(
            log.Data[req.XChannelId], log.Data[req.YChannelId], log.Data[req.ValueChannelId],
            req.XBins, req.YBins, agg, req.MinCount, filters);
        return new GetHistogramResult(req.LogId, new HistogramDto(grid.XEdges, grid.YEdges, grid.Cells, grid.Counts));
    }

    private GetSelectionStatsResult GetSelectionStats(GetSelectionStatsRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        foreach (int channelId in req.ChannelIds)
        {
            if (channelId < 0 || channelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {channelId}.");
        }

        var filters = BuildFilters(req.Filters, log);
        var stats = new ChannelStatsDto[req.ChannelIds.Length];
        for (int i = 0; i < req.ChannelIds.Length; i++)
        {
            int channelId = req.ChannelIds[i];
            var s = SelectionStats.Compute(log.Time, log.Data[channelId], req.T0, req.T1, filters);
            stats[i] = new ChannelStatsDto(
                channelId, s.Min, s.Max, s.Avg, s.Delta,
                s.StdDev, s.Variance, s.Median, s.P95,
                s.Count);
        }
        return new GetSelectionStatsResult(req.LogId, stats);
    }

    private ExportRangeResult ExportRange(ExportRangeRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        foreach (int channelId in req.ChannelIds)
        {
            if (channelId < 0 || channelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {channelId}.");
        }

        return new ExportRangeResult(CsvExport.Build(log, req.ChannelIds, req.T0, req.T1));
    }

    private ListMarksResult ListMarks(ListMarksRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        return new ListMarksResult(LogMarks.Find(log));
    }

    private AnnotationsResult ListAnnotations(ListAnnotationsRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        return new AnnotationsResult(annotations.List(log.SourcePath));
    }

    private AnnotationsResult AddAnnotation(AddAnnotationRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        return new AnnotationsResult(annotations.Add(log.SourcePath, req.Time, req.Text));
    }

    private AnnotationsResult DeleteAnnotation(DeleteAnnotationRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        return new AnnotationsResult(annotations.Delete(log.SourcePath, req.Id));
    }

    private GetRawSeriesResult GetRawSeries(GetRawSeriesRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        foreach (int channelId in req.ChannelIds)
        {
            if (channelId < 0 || channelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {channelId}.");
        }

        var (time, values) = RawSeries.Slice(log, req.ChannelIds, req.T0, req.T1);
        var series = new RawSeriesDto[req.ChannelIds.Length];
        for (int i = 0; i < req.ChannelIds.Length; i++)
            series[i] = new RawSeriesDto(req.ChannelIds[i], values[i]);
        return new GetRawSeriesResult(req.LogId, time, series);
    }

    private static SampleFilter[] BuildFilters(FilterDto[]? dtos, Model.ParsedLog log)
    {
        if (dtos is null || dtos.Length == 0) return [];
        var result = new SampleFilter[dtos.Length];
        for (int i = 0; i < dtos.Length; i++)
        {
            var f = dtos[i];
            if (f.ChannelId < 0 || f.ChannelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {f.ChannelId}.");
            if (!Enum.TryParse<FilterOp>(f.Op, ignoreCase: true, out var op))
                throw new ProtocolException(ErrorCodes.BadRequest, $"Unknown filter op '{f.Op}' (gt|gte|lt|lte|eq|neq).");
            result[i] = new SampleFilter(log.Data[f.ChannelId], op, f.Value);
        }
        return result;
    }

    private GetScatterResult GetScatter(GetScatterRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        foreach (int channelId in (int[])[req.XChannelId, req.YChannelId])
        {
            if (channelId < 0 || channelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {channelId}.");
        }
        float[]? color = null;
        if (req.ColorChannelId is int colorId)
        {
            if (colorId < 0 || colorId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {colorId}.");
            color = log.Data[colorId];
        }

        var filters = BuildFilters(req.Filters, log);
        var points = ScatterSampler.Sample(
            log.Time, log.Data[req.XChannelId], log.Data[req.YChannelId], color,
            req.T0, req.T1, req.MaxPoints, filters);
        return new GetScatterResult(req.LogId, new ScatterDto(points.X, points.Y, points.V, points.TotalSamples));
    }

    private ListLayoutsResult SaveLayout(SaveLayoutRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ProtocolException(ErrorCodes.BadRequest, "Layout needs a name.");
        return new ListLayoutsResult(layouts.Save(new LayoutDto(req.Name.Trim(), req.Panes)));
    }

    private ChannelColorsResult SetChannelColor(SetChannelColorRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new ProtocolException(ErrorCodes.BadRequest, "Channel color needs a label.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.Color, "^#[0-9a-fA-F]{6}$"))
            throw new ProtocolException(ErrorCodes.BadRequest, $"Color must be a #rrggbb hex string, got '{req.Color}'.");
        return new ChannelColorsResult(channelColors.Set(req.Label, req.Color));
    }

    private CustomMathChannelsResult SaveCustomMathChannel(SaveCustomMathChannelRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ProtocolException(ErrorCodes.BadRequest, "Custom math channel needs a name.");
        try
        {
            ExpressionParser.Parse(req.Expression);
        }
        catch (ExpressionParseException ex)
        {
            throw new ProtocolException(ErrorCodes.ExpressionError, ex.Message);
        }
        return new CustomMathChannelsResult(
            ToCustomMathChannelDtos(customMathChannels.Save(new CustomMathChannel(req.Name.Trim(), req.Unit.Trim(), req.Expression))));
    }

    private static CustomMathChannelDto[] ToCustomMathChannelDtos(List<CustomMathChannel> channels)
        => [.. channels.Select(c => new CustomMathChannelDto(c.Name, c.Unit, c.Expression))];

    private ListMathPresetsResult ListMathPresets(ListMathPresetsRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        return new ListMathPresetsResult(
            [.. MathPresets.All.Select(p =>
                new MathPresetDto(p.Id, p.Name, p.Unit, p.Expression, MathPresets.IsAvailable(p, log)))]);
    }

    private AddMathChannelResult AddMathChannel(AddMathChannelRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ProtocolException(ErrorCodes.BadRequest, "Math channel needs a name.");

        try
        {
            var expr = ExpressionParser.Parse(req.Expression);
            var values = Evaluator.Evaluate(expr, log);
            var info = log.AddComputedChannel(req.Name.Trim(), req.Unit.Trim(), values);
            return new AddMathChannelResult(req.LogId, ToDto(info));
        }
        catch (Exception ex) when (ex is ExpressionParseException or ExpressionEvalException)
        {
            throw new ProtocolException(ErrorCodes.ExpressionError, ex.Message);
        }
    }

    private GetSeriesResult GetSeries(GetSeriesRequest req)
    {
        var log = store.Get(req.LogId)
            ?? throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");

        foreach (int channelId in req.ChannelIds)
        {
            if (channelId < 0 || channelId >= log.Data.Length)
                throw new ProtocolException(ErrorCodes.UnknownChannel, $"No channel with id {channelId}.");
        }

        var decimated = new DecimatedSeries[req.ChannelIds.Length];
        Parallel.For(0, req.ChannelIds.Length, i =>
            decimated[i] = Decimator.Decimate(log.Time, log.Data[req.ChannelIds[i]], req.T0, req.T1, req.PixelWidth));

        var (t, columns) = SeriesAligner.Align(decimated);
        var series = new AlignedSeriesDto[req.ChannelIds.Length];
        for (int i = 0; i < series.Length; i++)
            series[i] = new AlignedSeriesDto(req.ChannelIds[i], columns[i]);
        return new GetSeriesResult(req.LogId, req.T0, req.T1, t, series);
    }

    private EmptyResult CloseLog(CloseLogRequest req)
    {
        if (!store.Remove(req.LogId))
            throw new ProtocolException(ErrorCodes.UnknownLog, $"No open log with id {req.LogId}.");
        return new EmptyResult();
    }

    private static RecentLogEntryDto[] ToRecentLogDtos(List<RecentLogEntry> entries)
        => [.. entries.Select(e => new RecentLogEntryDto(e.Path, e.OpenedAt, File.Exists(e.Path)))];

    private static string Error(long id, string code, string message)
        => JsonSerializer.Serialize(new { id, ok = false, error = new ErrorDto(code, message) }, JsonOptions);
}

public sealed class ProtocolException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
