using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class SelectionStatsTests
{
    [Fact]
    public void BasicRange_ComputesMinMaxAvg()
    {
        double[] t = [0, 1, 2, 3, 4];
        float[] v = [10, 20, 30, 40, 50];

        var s = SelectionStats.Compute(t, v, 1.0, 3.0);

        Assert.Equal(3, s.Count);
        Assert.Equal(20.0, s.Min);
        Assert.Equal(40.0, s.Max);
        Assert.Equal(30.0, s.Avg);
        Assert.Equal(20.0, s.Delta);
    }

    [Fact]
    public void Delta_IsLastMinusFirst_NotMaxMinusMin()
    {
        double[] t = [0, 1, 2, 3];
        float[] v = [30, 15, 22, 29];

        var s = SelectionStats.Compute(t, v, 0, 3);

        Assert.Equal(15.0, s.Min);
        Assert.Equal(30.0, s.Max);
        Assert.Equal(-1.0, s.Delta);
    }

    [Fact]
    public void NaNSamples_AreExcluded()
    {
        double[] t = [0, 1, 2];
        float[] v = [10, float.NaN, 30];

        var s = SelectionStats.Compute(t, v, 0, 2);

        Assert.Equal(2, s.Count);
        Assert.Equal(10.0, s.Min);
        Assert.Equal(30.0, s.Max);
        Assert.Equal(20.0, s.Avg);
    }

    [Fact]
    public void Filters_ExcludeSamplesFromAggregation()
    {
        double[] t = [0, 1, 2, 3];
        float[] v = [10, 20, 30, 40];
        float[] accel = [10, 95, 10, 95];

        var filters = new[] { new SampleFilter(accel, FilterOp.Gt, 90) };
        var s = SelectionStats.Compute(t, v, 0, 3, filters);

        Assert.Equal(2, s.Count);
        Assert.Equal(20.0, s.Min);
        Assert.Equal(40.0, s.Max);
        Assert.Equal(30.0, s.Avg);
        Assert.Equal(20.0, s.Delta);
    }

    [Fact]
    public void EmptyRange_ReturnsNullsNotCrash()
    {
        double[] t = [0, 1, 2];
        float[] v = [10, 20, 30];

        var s = SelectionStats.Compute(t, v, 5, 6);

        Assert.Equal(0, s.Count);
        Assert.Null(s.Min);
        Assert.Null(s.Max);
        Assert.Null(s.Avg);
        Assert.Null(s.Delta);
    }

    [Fact]
    public void AllNaNInRange_ReturnsNullsNotCrash()
    {
        double[] t = [0, 1, 2];
        float[] v = [float.NaN, float.NaN, float.NaN];

        var s = SelectionStats.Compute(t, v, 0, 2);

        Assert.Equal(0, s.Count);
        Assert.Null(s.Min);
        Assert.Null(s.StdDev);
        Assert.Null(s.Median);
    }

    [Fact]
    public void StdDevAndVariance_MatchKnownPopulationValues()
    {
        double[] t = [0, 1, 2, 3, 4, 5, 6, 7];
        float[] v = [2, 4, 4, 4, 5, 5, 7, 9];

        var s = SelectionStats.Compute(t, v, 0, 7);

        Assert.Equal(5.0, s.Avg);
        Assert.Equal(4.0, s.Variance!.Value, precision: 9);
        Assert.Equal(2.0, s.StdDev!.Value, precision: 9);
    }

    [Fact]
    public void Median_OddCount_IsMiddleValue()
    {
        double[] t = [0, 1, 2, 3, 4];
        float[] v = [10, 30, 20, 50, 40];

        var s = SelectionStats.Compute(t, v, 0, 4);

        Assert.Equal(30.0, s.Median);
    }

    [Fact]
    public void Median_EvenCount_AveragesTwoMiddleValues()
    {
        double[] t = [0, 1, 2, 3];
        float[] v = [10, 20, 30, 40];

        var s = SelectionStats.Compute(t, v, 0, 3);

        Assert.Equal(25.0, s.Median);
    }

    [Fact]
    public void P95_IgnoresSingleOutlierMoreThanMax()
    {
        double[] t = new double[21];
        float[] v = new float[21];
        for (int i = 0; i < 20; i++)
        {
            t[i] = i;
            v[i] = 10;
        }
        t[20] = 20;
        v[20] = 1000;

        var s = SelectionStats.Compute(t, v, 0, 20);

        Assert.Equal(1000.0, s.Max);
        Assert.True(s.P95 < 100, $"expected P95 well below the outlier, got {s.P95}");
    }

    [Fact]
    public void SingleSample_StatsAllEqualThatValue()
    {
        double[] t = [0];
        float[] v = [42];

        var s = SelectionStats.Compute(t, v, -0.5, 0.5);

        Assert.Equal(1, s.Count);
        Assert.Equal(42.0, s.Min);
        Assert.Equal(42.0, s.Max);
        Assert.Equal(42.0, s.Median);
        Assert.Equal(42.0, s.P95);
        Assert.Equal(0.0, s.StdDev);
        Assert.Equal(0.0, s.Delta);
    }
}
