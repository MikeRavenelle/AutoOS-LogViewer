namespace OpenLogViewer.Engine.Series;

public sealed record ChannelStats(
    double? Min, double? Max, double? Avg, double? Delta,
    double? StdDev, double? Variance, double? Median, double? P95,
    int Count);

public static class SelectionStats
{
    private static readonly ChannelStats Empty = new(null, null, null, null, null, null, null, null, 0);

    public static ChannelStats Compute(double[] time, float[] values, double t0, double t1, SampleFilter[]? filters = null)
    {
        var (first, last) = TimeIndex.RangeIndices(time, t0, t1);
        if (first < 0) return Empty;

        bool hasFilters = filters is { Length: > 0 };
        var surviving = new List<double>(last - first + 1);
        double firstValue = 0, lastValue = 0;
        for (int i = first; i <= last; i++)
        {
            float v = values[i];
            if (float.IsNaN(v)) continue;
            if (hasFilters && !SampleFilters.Keep(filters!, i)) continue;
            if (surviving.Count == 0) firstValue = v;
            lastValue = v;
            surviving.Add(v);
        }

        int count = surviving.Count;
        if (count == 0) return Empty;

        double min = surviving[0], max = surviving[0], sum = 0;
        foreach (double v in surviving)
        {
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }
        double avg = sum / count;

        double sqDiffSum = 0;
        foreach (double v in surviving) sqDiffSum += (v - avg) * (v - avg);
        double variance = sqDiffSum / count;
        double stdDev = Math.Sqrt(variance);

        surviving.Sort();
        double median = Percentile(surviving, 50);
        double p95 = Percentile(surviving, 95);

        return new ChannelStats(min, max, avg, lastValue - firstValue, stdDev, variance, median, p95, count);
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        double rank = p / 100.0 * (sorted.Count - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        double frac = rank - lo;
        return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
    }
}
