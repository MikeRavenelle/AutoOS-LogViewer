using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherGetRawSeriesTests
{
    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "fixtures", name);
    }

    private static JsonElement Send(CommandDispatcher d, int id, string cmd, object payload)
    {
        var request = JsonSerializer.Serialize(new { id, cmd, payload }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(d.Handle(request));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean(), doc.RootElement.ToString());
        return doc.RootElement.GetProperty("result").Clone();
    }

    private static CommandDispatcher NewDispatcher()
        => new(ParserRegistry.CreateDefault(), new LogStore(),
            new LayoutStore(Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json")),
            new AnnotationStore(Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json")),
            new ChannelColorStore(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json")),
            new RecentLogsStore(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json")),
            new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json")));

    [Fact]
    public void GetRawSeries_FullResolution_MatchesSampleCount()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();
        int boostError = opened.GetProperty("channels").EnumerateArray()
            .First(c => c.GetProperty("name").GetString()!.StartsWith("Boost Error"))
            .GetProperty("id").GetInt32();
        var timeRange = opened.GetProperty("timeRange");
        double t0 = timeRange.GetProperty("start").GetDouble();
        double t1 = timeRange.GetProperty("end").GetDouble();
        int sampleCount = opened.GetProperty("sampleCount").GetInt32();

        var result = Send(dispatcher, 2, "getRawSeries", new { logId, channelIds = new[] { boostError }, t0, t1 });

        Assert.Equal(sampleCount, result.GetProperty("t").GetArrayLength());
        var series = result.GetProperty("series")[0];
        Assert.Equal(boostError, series.GetProperty("channelId").GetInt32());
        Assert.Equal(sampleCount, series.GetProperty("v").GetArrayLength());
    }

    [Fact]
    public void GetRawSeries_UnknownChannel_ErrorsCleanly()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();

        var request = JsonSerializer.Serialize(new
        {
            id = 2,
            cmd = "getRawSeries",
            payload = new { logId, channelIds = new[] { 9999 }, t0 = 0.0, t1 = 1.0 },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("unknownChannel", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
