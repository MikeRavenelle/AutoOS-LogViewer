using OpenLogViewer.Engine.Model;
using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class LogMarksTests
{
    private static ParsedLog NewLog(double[] time, float[]? mark, string markName = "Log Mark") => new()
    {
        SourcePath = "test.csv",
        ParserId = "test",
        Metadata = new Dictionary<string, string>(),
        Time = time,
        Channels = mark is null
            ? [new ChannelInfo(0, "Engine Speed", "rpm")]
            : [new ChannelInfo(0, "Engine Speed", "rpm"), new ChannelInfo(1, markName, "")],
        Data = mark is null ? [time.Select(_ => 0f).ToArray()] : [time.Select(_ => 0f).ToArray(), mark],
    };

    [Fact]
    public void NonZeroSamples_ReturnTheirTimestamps()
    {
        var log = NewLog([0, 1, 2, 3], [0, 1, 0, 1]);

        var times = LogMarks.Find(log);

        Assert.Equal([1.0, 3.0], times);
    }

    [Fact]
    public void NoMarkChannel_ReturnsEmpty()
    {
        var log = NewLog([0, 1, 2], null);

        Assert.Empty(LogMarks.Find(log));
    }

    [Fact]
    public void AllZero_ReturnsEmpty()
    {
        var log = NewLog([0, 1, 2], [0, 0, 0]);

        Assert.Empty(LogMarks.Find(log));
    }

    [Fact]
    public void NaNSamples_AreIgnored()
    {
        var log = NewLog([0, 1, 2], [float.NaN, 1, float.NaN]);

        Assert.Equal([1.0], LogMarks.Find(log));
    }
}
