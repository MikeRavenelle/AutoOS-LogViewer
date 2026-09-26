using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherListMarksTests
{
    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "fixtures", name);
    }

    [Fact]
    public void ListMarks_NoMarksSet_ReturnsEmptyNotError()
    {
        var dispatcher = new CommandDispatcher(ParserRegistry.CreateDefault(), new LogStore(),
            new LayoutStore(Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json")),
            new AnnotationStore(Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json")),
            new ChannelColorStore(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json")),
            new RecentLogsStore(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json")),
            new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json")));

        var openReq = JsonSerializer.Serialize(new { id = 1, cmd = "openLog", payload = new { path = Fixture("2026-07-11_run1_leaking.csv") } }, CommandDispatcher.JsonOptions);
        var openDoc = JsonDocument.Parse(dispatcher.Handle(openReq));
        int logId = openDoc.RootElement.GetProperty("result").GetProperty("logId").GetInt32();

        var req = JsonSerializer.Serialize(new { id = 2, cmd = "listMarks", payload = new { logId } }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(req));

        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Empty(doc.RootElement.GetProperty("result").GetProperty("times").EnumerateArray());
    }
}
