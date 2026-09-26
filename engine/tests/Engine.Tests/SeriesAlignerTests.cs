using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class SeriesAlignerTests
{
    [Fact]
    public void SingleSeries_PassesThroughDense()
    {
        var (t, cols) = SeriesAligner.Align([new DecimatedSeries([1, 2, 3], [10f, 20f, 30f])]);
        Assert.Equal([1.0, 2, 3], t);
        Assert.Equal([10f, 20f, 30f], cols[0].Select(v => v!.Value));
    }

    [Fact]
    public void UnionMerge_PadsWithNulls()
    {
        var a = new DecimatedSeries([1, 3, 5], [1f, 3f, 5f]);
        var b = new DecimatedSeries([2, 3, 4], [20f, 30f, 40f]);
        var (t, cols) = SeriesAligner.Align([a, b]);

        Assert.Equal([1.0, 2, 3, 4, 5], t);
        Assert.Equal([1f, null, 3f, null, 5f], cols[0]);
        Assert.Equal([null, 20f, 30f, 40f, null], cols[1]);
    }

    [Fact]
    public void SharedTimestamps_AreNotDuplicated()
    {
        var a = new DecimatedSeries([1, 2], [1f, 2f]);
        var b = new DecimatedSeries([1, 2], [10f, 20f]);
        var (t, cols) = SeriesAligner.Align([a, b]);

        Assert.Equal([1.0, 2], t);
        Assert.Equal([1f, 2f], cols[0]);
        Assert.Equal([10f, 20f], cols[1]);
    }

    [Fact]
    public void TimeAxisIsStrictlyAscending()
    {
        var a = new DecimatedSeries([0, 0.5, 2.5], [1f, 2f, 3f]);
        var b = new DecimatedSeries([0.25, 0.5, 3], [4f, 5f, 6f]);
        var c = new DecimatedSeries([0, 3], [7f, 8f]);
        var (t, cols) = SeriesAligner.Align([a, b, c]);

        for (int i = 1; i < t.Length; i++)
            Assert.True(t[i] > t[i - 1]);
        Assert.All(cols, col => Assert.Equal(t.Length, col.Length));
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        var (t, cols) = SeriesAligner.Align([]);
        Assert.Empty(t);
        Assert.Empty(cols);
    }
}
