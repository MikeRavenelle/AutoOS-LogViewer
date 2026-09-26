namespace OpenLogViewer.Engine.Series;

internal static class TimeIndex
{
    public static int LowerBound(double[] time, double t)
    {
        int idx = Array.BinarySearch(time, t);
        if (idx < 0) idx = ~idx;
        return Math.Min(idx, time.Length - 1);
    }

    public static (int First, int Last) RangeIndices(double[] time, double t0, double t1)
    {
        if (time.Length == 0 || t1 <= t0 || t0 > time[^1] || t1 < time[0])
            return (-1, -1);

        int first = LowerBound(time, t0);
        int last = LowerBound(time, t1);
        if (last < time.Length && time[last] > t1) last--;
        return last < first ? (-1, -1) : (first, last);
    }
}
