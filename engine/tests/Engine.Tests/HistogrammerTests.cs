using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class HistogrammerTests
{
    [Fact]
    public void Mean_BinsSamplesIntoCorrectCells()
    {
        float[] x = [1, 8, 1, 8, 9];
        float[] y = [1, 1, 8, 8, 9];
        float[] v = [10, 20, 30, 40, 60];

        var g = Histogrammer.Compute(x, y, v, 2, 2, HistogramAgg.Mean, 1);

        Assert.Equal(3, g.XEdges.Length);
        Assert.Equal(10.0, g.Cells[0][0]);
        Assert.Equal(20.0, g.Cells[0][1]);
        Assert.Equal(30.0, g.Cells[1][0]);
        Assert.Equal(50.0, g.Cells[1][1]);
        Assert.Equal(2, g.Counts[1][1]);
    }

    [Fact]
    public void MinCountFilter_NullsSparseCellsButKeepsCounts()
    {
        float[] x = [1, 8, 8];
        float[] y = [1, 8, 8];
        float[] v = [5, 7, 9];

        var g = Histogrammer.Compute(x, y, v, 2, 2, HistogramAgg.Mean, 2);

        Assert.Null(g.Cells[0][0]);
        Assert.Equal(1, g.Counts[0][0]);
        Assert.Equal(8.0, g.Cells[1][1]);
    }

    [Fact]
    public void MinMaxCountAggs()
    {
        float[] x = [1, 1, 1];
        float[] y = [1, 1, 1];
        float[] v = [3, 9, 6];

        Assert.Equal(3.0, Histogrammer.Compute(x, y, v, 1, 1, HistogramAgg.Min, 1).Cells[0][0]);
        Assert.Equal(9.0, Histogrammer.Compute(x, y, v, 1, 1, HistogramAgg.Max, 1).Cells[0][0]);
        Assert.Equal(3.0, Histogrammer.Compute(x, y, v, 1, 1, HistogramAgg.Count, 1).Cells[0][0]);
    }

    [Fact]
    public void NaNSamples_AreExcluded()
    {
        float[] x = [1, float.NaN, 1];
        float[] y = [1, 1, 1];
        float[] v = [5, 100, float.NaN];

        var g = Histogrammer.Compute(x, y, v, 1, 1, HistogramAgg.Mean, 1);
        Assert.Equal(5.0, g.Cells[0][0]);
        Assert.Equal(1, g.Counts[0][0]);
    }

    [Fact]
    public void MaxValueLandsInLastBin()
    {
        float[] x = [0, 10];
        float[] y = [0, 10];
        float[] v = [1, 2];
        var g = Histogrammer.Compute(x, y, v, 4, 4, HistogramAgg.Mean, 1);
        Assert.Equal(2.0, g.Cells[3][3]);
    }

    [Fact]
    public void Filters_ExcludeSamplesBeforeBinningAndRange()
    {
        float[] x = [1, 1, 100, 100];
        float[] y = [1, 1, 1, 1];
        float[] v = [10, 20, 30, 40];
        float[] accel = [10, 10, 95, 95];

        var filters = new[] { new Series.SampleFilter(accel, Series.FilterOp.Gt, 90) };
        var g = Histogrammer.Compute(x, y, v, 1, 1, HistogramAgg.Mean, 1, filters);

        Assert.Equal(100.0, g.XEdges[0]);
        Assert.Equal(35.0, g.Cells[0][0]);
        Assert.Equal(2, g.Counts[0][0]);
    }

    [Fact]
    public void AllNaN_ReturnsEmptyGridNotCrash()
    {
        float[] nan = [float.NaN, float.NaN];
        var g = Histogrammer.Compute(nan, nan, nan, 4, 4, HistogramAgg.Mean, 1);
        Assert.All(g.Cells, row => Assert.All(row, c => Assert.Null(c)));
    }
}
