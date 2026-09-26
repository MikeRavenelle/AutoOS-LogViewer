using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherOpenLogTests
{
    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "fixtures", name);
    }

    [Fact]
    public void OpenLog_MaterializesPresetsAsAdditionalChannels()
    {
        var dispatcher = new CommandDispatcher(ParserRegistry.CreateDefault(), new LogStore(),
            new LayoutStore(Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json")),
            new AnnotationStore(Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json")),
            new ChannelColorStore(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json")),
            new RecentLogsStore(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json")),
            new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json")));
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            cmd = "openLog",
            payload = new { path = Fixture("2026-07-11_run1_leaking.csv") },
        }, CommandDispatcher.JsonOptions);
        var root = JsonDocument.Parse(dispatcher.Handle(request)).RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());

        var channels = root.GetProperty("result").GetProperty("channels").EnumerateArray().ToArray();

        var raw = channels.Where(c => !c.GetProperty("computed").GetBoolean()).ToArray();
        var computed = channels.Where(c => c.GetProperty("computed").GetBoolean())
            .Select(c => c.GetProperty("name").GetString()).ToArray();
        Assert.Equal(69, raw.Length);
        Assert.Contains("Gauge Boost", computed);
        Assert.Contains("Boost Target Gauge", computed);
        Assert.Contains("Lambda Delta", computed);
        Assert.Contains("Boost Error", computed);
        Assert.Contains(raw, c => c.GetProperty("name").GetString() == "Boost Pressure Actual Sensor 1");
    }
}
