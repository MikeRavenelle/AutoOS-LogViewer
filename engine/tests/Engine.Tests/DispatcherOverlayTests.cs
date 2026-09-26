using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherOverlayTests
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
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean(),
            doc.RootElement.ToString());
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
    public void Overlay_TwoFixtureLogs_SharedAxisAndOffset()
    {
        var dispatcher = NewDispatcher();

        var a = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        var b = Send(dispatcher, 2, "openLog", new { path = Fixture("2026-07-17_postfix_WOT_verification.csv") });
        int logA = a.GetProperty("logId").GetInt32();
        int logB = b.GetProperty("logId").GetInt32();
        Assert.NotEqual(logA, logB);

        int ChannelId(JsonElement open, string prefix) =>
            open.GetProperty("channels").EnumerateArray()
                .First(c => c.GetProperty("name").GetString()!.StartsWith(prefix))
                .GetProperty("id").GetInt32();
        int chA = ChannelId(a, "Boost Pressure Actual");
        int chB = ChannelId(b, "Boost Pressure Actual");
        Assert.NotEqual(chA, chB);

        const double offset = -350.0;
        var overlay = Send(dispatcher, 3, "getOverlaySeries", new
        {
            sources = new object[]
            {
                new { logId = logA, channelId = chA, offset = 0.0 },
                new { logId = logB, channelId = chB, offset },
            },
            t0 = 0.0,
            t1 = 11.0,
            pixelWidth = 800,
        });

        var t = overlay.GetProperty("t").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var series = overlay.GetProperty("series").EnumerateArray().ToArray();
        Assert.Equal(2, series.Length);
        Assert.True(t.Length > 10);
        for (int i = 1; i < t.Length; i++)
            Assert.True(t[i] > t[i - 1], "display axis must ascend");

        foreach (var s in series)
        {
            var v = s.GetProperty("v").EnumerateArray().ToArray();
            Assert.Equal(t.Length, v.Length);
            Assert.Contains(v, x => x.ValueKind == JsonValueKind.Number);
        }

        Assert.True(t[0] >= -1 && t[^1] <= 12);
    }

    [Fact]
    public void Overlay_UnknownLogOrChannel_ErrorsCleanly()
    {
        var dispatcher = NewDispatcher();
        var request = JsonSerializer.Serialize(new
        {
            id = 9,
            cmd = "getOverlaySeries",
            payload = new
            {
                sources = new[] { new { logId = 42, channelId = 0, offset = 0.0 } },
                t0 = 0.0,
                t1 = 1.0,
                pixelWidth = 100,
            },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("unknownLog", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
