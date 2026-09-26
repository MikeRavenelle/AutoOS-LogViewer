namespace OpenLogViewer.Engine.Series;

public sealed record ScatterPoints(float[] X, float[] Y, float?[] V, int TotalSamples);

public static class ScatterSampler
{
    public static ScatterPoints Sample(
        double[] time, float[] x, float[] y, float[]? color,
        double t0, double t1, int maxPoints, SampleFilter[]? filters = null)
    {
        maxPoints = Math.Clamp(maxPoints, 1, 20_000);
        var (first, last) = TimeIndex.RangeIndices(time, t0, t1);
        if (first < 0)
            return new ScatterPoints([], [], [], 0);

        bool hasFilters = filters is { Length: > 0 };
        var eligible = new List<int>(last - first + 1);
        for (int i = first; i <= last; i++)
        {
            if (float.IsNaN(x[i]) || float.IsNaN(y[i])) continue;
            if (color is not null && float.IsNaN(color[i])) continue;
            if (hasFilters && !SampleFilters.Keep(filters!, i)) continue;
            eligible.Add(i);
        }

        int total = eligible.Count;
        if (total == 0)
            return new ScatterPoints([], [], [], 0);

        int stride = Math.Max(1, (int)Math.Ceiling(total / (double)maxPoints));
        var xs = new List<float>();
        var ys = new List<float>();
        var vs = new List<float?>();
        for (int k = 0; k < eligible.Count; k += stride)
        {
            int i = eligible[k];
            xs.Add(x[i]);
            ys.Add(y[i]);
            vs.Add(color is not null ? color[i] : null);
        }

        return new ScatterPoints([.. xs], [.. ys], [.. vs], total);
    }
}
