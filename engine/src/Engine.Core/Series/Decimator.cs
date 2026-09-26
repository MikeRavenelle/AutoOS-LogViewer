namespace OpenLogViewer.Engine.Series;

public sealed record DecimatedSeries(double[] T, float[] V);

public static class Decimator
{
    public static DecimatedSeries Decimate(double[] time, float[] values, double t0, double t1, int pixelWidth)
    {
        if (time.Length == 0 || pixelWidth <= 0 || t1 <= t0)
            return new DecimatedSeries([], []);

        int first = TimeIndex.LowerBound(time, t0);
        int last = TimeIndex.LowerBound(time, t1);
        if (first > 0) first--;
        if (last < time.Length - 1) last++;
        int count = last - first + 1;
        if (count <= 0)
            return new DecimatedSeries([], []);

        if (count <= 2 * pixelWidth)
        {
            var rawT = new List<double>(count);
            var rawV = new List<float>(count);
            for (int k = first; k <= last; k++)
            {
                if (float.IsNaN(values[k])) continue;
                rawT.Add(time[k]);
                rawV.Add(values[k]);
            }
            return new DecimatedSeries([.. rawT], [.. rawV]);
        }

        var outT = new List<double>(2 * pixelWidth);
        var outV = new List<float>(2 * pixelWidth);
        double bucketWidth = (t1 - t0) / pixelWidth;

        int i = first;
        for (int bucket = 0; bucket < pixelWidth && i <= last; bucket++)
        {
            double bucketEnd = t0 + (bucket + 1) * bucketWidth;
            bool isLastBucket = bucket == pixelWidth - 1;

            int minIdx = -1, maxIdx = -1;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;

            for (; i <= last && (isLastBucket || time[i] < bucketEnd); i++)
            {
                float v = values[i];
                if (float.IsNaN(v)) continue;
                if (v < min) { min = v; minIdx = i; }
                if (v > max) { max = v; maxIdx = i; }
            }

            if (minIdx < 0) continue;

            int a = Math.Min(minIdx, maxIdx), b = Math.Max(minIdx, maxIdx);
            outT.Add(time[a]);
            outV.Add(values[a]);
            if (b != a)
            {
                outT.Add(time[b]);
                outV.Add(values[b]);
            }
        }

        return new DecimatedSeries([.. outT], [.. outV]);
    }
}
