using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherCustomMathChannelTests
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

    private static CommandDispatcher NewDispatcher(CustomMathChannelStore? customMathChannels = null)
        => new(ParserRegistry.CreateDefault(), new LogStore(),
            new LayoutStore(Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json")),
            new AnnotationStore(Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json")),
            new ChannelColorStore(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json")),
            new RecentLogsStore(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json")),
            customMathChannels ?? new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json")));

    [Fact]
    public void SaveListDelete_RoundTrips()
    {
        var dispatcher = NewDispatcher();

        var afterSave = Send(dispatcher, 1, "saveCustomMathChannel",
            new { name = "Boost Lag", unit = "psi", expression = "[Boost Error (RaceROM) (psi)]" });
        Assert.Single(afterSave.GetProperty("channels").EnumerateArray());

        var listed = Send(dispatcher, 2, "listCustomMathChannels", new { });
        Assert.Single(listed.GetProperty("channels").EnumerateArray());

        var afterDelete = Send(dispatcher, 3, "deleteCustomMathChannel", new { name = "Boost Lag" });
        Assert.Empty(afterDelete.GetProperty("channels").EnumerateArray());
    }

    [Fact]
    public void SaveCustomMathChannel_RejectsEmptyName()
    {
        var dispatcher = NewDispatcher();
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            cmd = "saveCustomMathChannel",
            payload = new { name = "", unit = "psi", expression = "[X]" },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("badRequest", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void SaveCustomMathChannel_RejectsMalformedExpression()
    {
        var dispatcher = NewDispatcher();
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            cmd = "saveCustomMathChannel",
            payload = new { name = "Bad", unit = "psi", expression = "[Unclosed" },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("expressionError", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void SavedChannel_AutoMaterializesOnEveryLogThatHasItsChannels()
    {
        var store = new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json"));
        store.Save(new CustomMathChannel("My Boost Error", "psi", "[Boost Error (RaceROM) (psi)]"));

        var dispatcher = NewDispatcher(store);
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });

        var channelNames = opened.GetProperty("channels").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("My Boost Error", channelNames);
    }

    [Fact]
    public void SavedChannel_SkippedSilently_WhenLogLacksReferencedChannel()
    {
        var store = new CustomMathChannelStore(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json"));
        store.Save(new CustomMathChannel("Nonexistent Combo", "psi", "[Totally Not A Real Channel (psi)]"));

        var dispatcher = NewDispatcher(store);
        var opened = Send(dispatcher, 1, "openLog", new { path = Fixture("2026-07-11_run1_leaking.csv") });

        var channelNames = opened.GetProperty("channels").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToArray();
        Assert.DoesNotContain("Nonexistent Combo", channelNames);
    }
}
