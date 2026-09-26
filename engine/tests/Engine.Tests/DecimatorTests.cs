using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class DecimatorTests
{
    private static (double[] t, float[] v) MakeSignal(int n, double dt, Func<int, float> f)
    {
        var t = new double[n];
        var v = new float[n];
        for (int i = 0; i < n; i++) { t[i] = i * dt; v[i] = f(i); }
        return (t, v);
    }

    [Fact]
    public void SmallRange_ReturnsRawSamples()
    {
        var (t, v) = MakeSignal(100, 0.1, i => i);
        var s = Decimator.Decimate(t, v, 0, 10, 500);
        Assert.Equal(100, s.T.Length);
        Assert.Equal(v, s.V);
    }

    [Fact]
    public void LargeRange_StaysUnderTwoPointsPerPixel()
    {
        var (t, v) = MakeSignal(36_000, 0.1, i => MathF.Sin(i * 0.01f));
        var s = Decimator.Decimate(t, v, 0, 3600, 800);
        Assert.True(s.T.Length <= 1600, $"got {s.T.Length} points");
        Assert.True(s.T.Length > 800, "should emit ~min+max per bucket");
    }

    [Fact]
    public void ShortTransientSurvivesAggressiveDecimation()
    {
        var (t, v) = MakeSignal(36_000, 0.1, i => i is >= 18_000 and < 18_003 ? 9.0f : 0.0f);
        var s = Decimator.Decimate(t, v, 0, 3600, 400);
        Assert.Contains(9.0f, s.V);
    }

    [Fact]
    public void PointsAreActualSamplesInChronologicalOrder()
    {
        var (t, v) = MakeSignal(10_000, 0.1, i => MathF.Sin(i * 0.7f) * 10);
        var s = Decimator.Decimate(t, v, 0, 1000, 300);

        for (int i = 1; i < s.T.Length; i++)
            Assert.True(s.T[i] > s.T[i - 1], "timestamps must ascend");

        for (int i = 0; i < s.T.Length; i++)
        {
            int idx = Array.BinarySearch(t, s.T[i]);
            Assert.True(idx >= 0, "point time not found in source");
            Assert.Equal(v[idx], s.V[i]);
        }
    }

    [Fact]
    public void IncludesOneSampleMarginBeyondViewport()
    {
        var (t, v) = MakeSignal(1000, 0.1, i => i);
        var s = Decimator.Decimate(t, v, 50.05, 60.05, 2000);
        Assert.True(s.T[0] < 50.05);
        Assert.True(s.T[^1] > 60.05);
    }

    [Fact]
    public void EmptyAndDegenerateInputsReturnEmpty()
    {
        Assert.Empty(Decimator.Decimate([], [], 0, 1, 100).T);
        var (t, v) = MakeSignal(10, 0.1, i => i);
        Assert.Empty(Decimator.Decimate(t, v, 5, 5, 100).T);
        Assert.Empty(Decimator.Decimate(t, v, 0, 1, 0).T);
    }
}
