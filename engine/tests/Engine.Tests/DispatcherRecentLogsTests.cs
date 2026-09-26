using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherRecentLogsTests
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
    public void OpenLog_AddsItToRecentLogs()
    {
        var dispatcher = NewDispatcher();
        var path = Fixture("2026-07-11_run1_leaking.csv");

        Send(dispatcher, 1, "openLog", new { path });
        var listed = Send(dispatcher, 2, "listRecentLogs", new { });

        var logs = listed.GetProperty("logs").EnumerateArray().ToArray();
        Assert.Single(logs);
        Assert.Equal(path, logs[0].GetProperty("path").GetString());
        Assert.True(logs[0].GetProperty("exists").GetBoolean());
    }

    [Fact]
    public void ListRecentLogs_MissingFile_ReportsExistsFalse()
    {
        var recentLogs = new RecentLogsStore(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json"));
        recentLogs.Touch("/does/not/exist.csv");
        var dispatcher = new CommandDispatcher(ParserRegistry.CreateDefault(), new LogStore(),
            new LayoutStore(Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json")),
            new AnnotationStore(Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json")),
            new ChannelColorStore(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json")),
            recentLogs,
            new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json")));

        var listed = Send(dispatcher, 1, "listRecentLogs", new { });

        var logs = listed.GetProperty("logs").EnumerateArray().ToArray();
        Assert.Single(logs);
        Assert.False(logs[0].GetProperty("exists").GetBoolean());
    }

    [Fact]
    public void RemoveRecentLog_DropsIt()
    {
        var dispatcher = NewDispatcher();
        var path = Fixture("2026-07-11_run1_leaking.csv");
        Send(dispatcher, 1, "openLog", new { path });

        var afterRemove = Send(dispatcher, 2, "removeRecentLog", new { path });

        Assert.Empty(afterRemove.GetProperty("logs").EnumerateArray());
    }
}
