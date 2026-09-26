using System.Text.Json;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DispatcherChannelColorTests
{
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
    public void SetListClear_RoundTrips()
    {
        var dispatcher = NewDispatcher();

        var afterSet = Send(dispatcher, 1, "setChannelColor", new { label = "Boost Error (psi)", color = "#ff2d55" });
        Assert.Equal("#ff2d55", afterSet.GetProperty("colors").GetProperty("Boost Error (psi)").GetString());

        var listed = Send(dispatcher, 2, "listChannelColors", new { });
        Assert.Equal("#ff2d55", listed.GetProperty("colors").GetProperty("Boost Error (psi)").GetString());

        var afterClear = Send(dispatcher, 3, "clearChannelColor", new { label = "Boost Error (psi)" });
        Assert.False(afterClear.GetProperty("colors").TryGetProperty("Boost Error (psi)", out _));
    }

    [Fact]
    public void SetChannelColor_RejectsNonHexColor()
    {
        var dispatcher = NewDispatcher();
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            cmd = "setChannelColor",
            payload = new { label = "RPM", color = "blue" },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("badRequest", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void SetChannelColor_RejectsEmptyLabel()
    {
        var dispatcher = NewDispatcher();
        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            cmd = "setChannelColor",
            payload = new { label = "", color = "#ffffff" },
        }, CommandDispatcher.JsonOptions);
        var doc = JsonDocument.Parse(dispatcher.Handle(request));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("badRequest", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
