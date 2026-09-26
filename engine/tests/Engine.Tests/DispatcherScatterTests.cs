using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherScatterTests
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
    public void Scatter_RpmVsBoostError_ReturnsBoundedPointCloud()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();

        int ChannelId(string prefix) =>
            opened.GetProperty("channels").EnumerateArray()
                .First(c => c.GetProperty("name").GetString()!.StartsWith(prefix))
                .GetProperty("id").GetInt32();
        int rpm = ChannelId("Engine Speed");
        int boostError = ChannelId("Boost Error");
        var timeRange = opened.GetProperty("timeRange");
        double t0 = timeRange.GetProperty("start").GetDouble();
        double t1 = timeRange.GetProperty("end").GetDouble();

        var result = Send(dispatcher, 2, "getScatter", new
        {
            logId,
            xChannelId = rpm,
            yChannelId = boostError,
            colorChannelId = (int?)null,
            t0,
            t1,
            maxPoints = 500,
        });

        var scatter = result.GetProperty("scatter");
        var x = scatter.GetProperty("x").EnumerateArray().ToArray();
        var y = scatter.GetProperty("y").EnumerateArray().ToArray();
        var v = scatter.GetProperty("v").EnumerateArray().ToArray();
        Assert.True(x.Length > 0);
        Assert.True(x.Length <= 500);
        Assert.Equal(x.Length, y.Length);
        Assert.Equal(x.Length, v.Length);
        Assert.All(v, e => Assert.Equal(JsonValueKind.Null, e.ValueKind));
        Assert.True(scatter.GetProperty("totalSamples").GetInt32() >= x.Length);
    }

    [Fact]
    public void Scatter_WotFilter_ShrinksPointCloud()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();

        int ChannelId(string prefix) =>
            opened.GetProperty("channels").EnumerateArray()
                .First(c => c.GetProperty("name").GetString()!.StartsWith(prefix))
                .GetProperty("id").GetInt32();
        int rpm = ChannelId("Engine Speed");
        int boostError = ChannelId("Boost Error");
        int accel = ChannelId("Accelerator Position");
        var timeRange = opened.GetProperty("timeRange");
        double t0 = timeRange.GetProperty("start").GetDouble();
        double t1 = timeRange.GetProperty("end").GetDouble();

        var unfiltered = Send(dispatcher, 2, "getScatter", new
        {
            logId, xChannelId = rpm, yChannelId = boostError, colorChannelId = (int?)null,
            t0, t1, maxPoints = 5000,
        });
        var filtered = Send(dispatcher, 3, "getScatter", new
        {
            logId, xChannelId = rpm, yChannelId = boostError, colorChannelId = (int?)null,
            t0, t1, maxPoints = 5000,
            filters = new object[] { new { channelId = accel, op = "gt", value = 90.0 } },
        });

        int unfilteredTotal = unfiltered.GetProperty("scatter").GetProperty("totalSamples").GetInt32();
        int filteredTotal = filtered.GetProperty("scatter").GetProperty("totalSamples").GetInt32();
        Assert.True(filteredTotal < unfilteredTotal, $"WOT filter should shrink the point cloud ({filteredTotal} vs {unfilteredTotal})");
        Assert.True(filteredTotal > 0);
    }

    [Fact]
    public void Scatter_UnknownFilterOp_ErrorsCleanly()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();
        int rpm = opened.GetProperty("channels").EnumerateArray().First().GetProperty("id").GetInt32();

        var request = JsonSerializer.Serialize(new
        {
            id = 2,
            cmd = "getScatter",
            payload = new
            {
                logId, xChannelId = rpm, yChannelId = rpm, colorChannelId = (int?)null,
                t0 = 0.0, t1 = 1.0, maxPoints = 100,
                filters = new object[] { new { channelId = rpm, op = "between", value = 1.0 } },
            },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("badRequest", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Scatter_UnknownChannel_ErrorsCleanly()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();

        var request = JsonSerializer.Serialize(new
        {
            id = 2,
            cmd = "getScatter",
            payload = new { logId, xChannelId = 9999, yChannelId = 0, colorChannelId = (int?)null, t0 = 0.0, t1 = 1.0, maxPoints = 100 },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("unknownChannel", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
