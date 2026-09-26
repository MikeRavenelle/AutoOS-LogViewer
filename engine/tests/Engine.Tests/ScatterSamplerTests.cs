using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class ScatterSamplerTests
{
    [Fact]
    public void FewerThanMax_ReturnsAllSamplesInRange()
    {
        double[] t = [0, 1, 2, 3, 4];
        float[] x = [10, 11, 12, 13, 14];
        float[] y = [1, 2, 3, 4, 5];

        var p = ScatterSampler.Sample(t, x, y, null, 1.0, 3.0, 100);

        Assert.Equal(3, p.TotalSamples);
        Assert.Equal(3, p.X.Length);
        Assert.Equal([11, 12, 13], p.X);
        Assert.All(p.V, v => Assert.Null(v));
    }

    [Fact]
    public void MoreThanMax_UniformlyStrides()
    {
        double[] t = new double[1000];
        float[] x = new float[1000];
        float[] y = new float[1000];
        for (int i = 0; i < 1000; i++) { t[i] = i; x[i] = i; y[i] = i; }

        var p = ScatterSampler.Sample(t, x, y, null, 0, 999, 100);

        Assert.Equal(1000, p.TotalSamples);
        Assert.True(p.X.Length <= 100);
        Assert.True(p.X.Length > 0);
        for (int i = 1; i < p.X.Length; i++) Assert.True(p.X[i] > p.X[i - 1]);
    }

    [Fact]
    public void ColorChannel_CarriesThirdValue()
    {
        double[] t = [0, 1, 2];
        float[] x = [1, 2, 3];
        float[] y = [4, 5, 6];
        float[] color = [100, 200, 300];

        var p = ScatterSampler.Sample(t, x, y, color, 0, 2, 100);

        Assert.Equal([100f, 200f, 300f], p.V);
    }

    [Fact]
    public void NaNInXYOrColor_ExcludesSample()
    {
        double[] t = [0, 1, 2, 3];
        float[] x = [1, float.NaN, 3, 4];
        float[] y = [1, 2, float.NaN, 4];
        float[] color = [1, 2, 3, float.NaN];

        var p = ScatterSampler.Sample(t, x, y, color, 0, 3, 100);

        Assert.Single(p.X);
        Assert.Equal(1f, p.X[0]);
    }

    [Fact]
    public void Filters_ExcludeSamplesAndShrinkTotalSamples()
    {
        double[] t = [0, 1, 2, 3];
        float[] x = [1, 2, 3, 4];
        float[] y = [10, 20, 30, 40];
        float[] accel = [10, 95, 10, 95];

        var filters = new[] { new SampleFilter(accel, FilterOp.Gt, 90) };
        var p = ScatterSampler.Sample(t, x, y, null, 0, 3, 100, filters);

        Assert.Equal(2, p.TotalSamples);
        Assert.Equal([2f, 4f], p.X);
        Assert.Equal([20f, 40f], p.Y);
    }

    [Fact]
    public void EmptyRange_ReturnsEmptyNotCrash()
    {
        double[] t = [0, 1, 2];
        float[] x = [1, 2, 3];
        float[] y = [1, 2, 3];

        var p = ScatterSampler.Sample(t, x, y, null, 5, 6, 100);

        Assert.Equal(0, p.TotalSamples);
        Assert.Empty(p.X);
    }
}
