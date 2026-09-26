using OpenLogViewer.Engine.Model;
using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class RawSeriesTests
{
    private static ParsedLog NewLog(double[] time, float[] a, float[] b) => new()
    {
        SourcePath = "test.csv",
        ParserId = "test",
        Metadata = new Dictionary<string, string>(),
        Time = time,
        Channels = [new ChannelInfo(0, "A", "psi"), new ChannelInfo(1, "B", "rpm")],
        Data = [a, b],
    };

    [Fact]
    public void BasicRange_SlicesEveryChannelInLockstep()
    {
        var log = NewLog([0, 1, 2, 3], [10, 20, 30, 40], [1, 2, 3, 4]);

        var (time, values) = RawSeries.Slice(log, [0, 1], 1.0, 2.0);

        Assert.Equal([1.0, 2.0], time);
        Assert.Equal([20f, 30f], values[0]);
        Assert.Equal([2f, 3f], values[1]);
    }

    [Fact]
    public void EmptyRange_ReturnsEmptyArraysNotNull()
    {
        var log = NewLog([0, 1, 2], [10, 20, 30], [1, 2, 3]);

        var (time, values) = RawSeries.Slice(log, [0, 1], 5, 6);

        Assert.Empty(time);
        Assert.Empty(values[0]);
        Assert.Empty(values[1]);
    }

    [Fact]
    public void FullResolution_NoDecimation()
    {
        var time = Enumerable.Range(0, 100).Select(i => i * 0.1).ToArray();
        var a = Enumerable.Range(0, 100).Select(i => (float)i).ToArray();
        var log = NewLog(time, a, a);

        var (sliced, values) = RawSeries.Slice(log, [0], 0, 9.9);

        Assert.Equal(100, sliced.Length);
        Assert.Equal(100, values[0].Length);
    }
}
