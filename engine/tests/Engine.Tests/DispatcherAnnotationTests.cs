using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherAnnotationTests
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
    public void AddListDelete_RoundTrips()
    {
        var dispatcher = NewDispatcher();
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });
        int logId = opened.GetProperty("logId").GetInt32();

        var added = Send(dispatcher, 2, "addAnnotation", new { logId, time = 4.2, text = "boost dip here" });
        var afterAdd = added.GetProperty("annotations");
        Assert.Equal(1, afterAdd.GetArrayLength());
        string id = afterAdd[0].GetProperty("id").GetString()!;

        var listed = Send(dispatcher, 3, "listAnnotations", new { logId });
        Assert.Equal(1, listed.GetProperty("annotations").GetArrayLength());

        var deleted = Send(dispatcher, 4, "deleteAnnotation", new { logId, id });
        Assert.Equal(0, deleted.GetProperty("annotations").GetArrayLength());
    }

    [Fact]
    public void ReopeningSameFile_ReloadsAnnotations()
    {
        var layouts = new LayoutStore(Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json"));
        var annotationsPath = Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json");
        var path = Fixture("2026-07-11_run1_leaking.csv");

        var colors = new ChannelColorStore(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json"));
        var recentLogs = new RecentLogsStore(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json"));
        var customMathChannels = new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json"));
        var d1 = new CommandDispatcher(ParserRegistry.CreateDefault(), new LogStore(), layouts, new AnnotationStore(annotationsPath), colors, recentLogs, customMathChannels);
        var opened1 = Send(d1, 1, "openLog", new { path });
        Send(d1, 2, "addAnnotation", new { logId = opened1.GetProperty("logId").GetInt32(), time = 1.0, text = "note" });

        var d2 = new CommandDispatcher(ParserRegistry.CreateDefault(), new LogStore(), layouts, new AnnotationStore(annotationsPath), colors, recentLogs, customMathChannels);
        var opened2 = Send(d2, 1, "openLog", new { path });
        var listed = Send(d2, 2, "listAnnotations", new { logId = opened2.GetProperty("logId").GetInt32() });

        Assert.Equal(1, listed.GetProperty("annotations").GetArrayLength());
        Assert.Equal("note", listed.GetProperty("annotations")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void UnknownLog_ErrorsCleanly()
    {
        var dispatcher = NewDispatcher();
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            cmd = "addAnnotation",
            payload = new { logId = 9999, time = 0.0, text = "x" },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("unknownLog", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
