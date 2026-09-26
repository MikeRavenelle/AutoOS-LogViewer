using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Series;

public static class RawSeries
{
    public static (double[] Time, float[][] Values) Slice(ParsedLog log, int[] channelIds, double t0, double t1)
    {
        var (first, last) = TimeIndex.RangeIndices(log.Time, t0, t1);
        if (first < 0)
            return ([], [.. channelIds.Select(_ => Array.Empty<float>())]);

        int count = last - first + 1;
        var time = new double[count];
        Array.Copy(log.Time, first, time, 0, count);

        var values = new float[channelIds.Length][];
        for (int c = 0; c < channelIds.Length; c++)
        {
            values[c] = new float[count];
            Array.Copy(log.Data[channelIds[c]], first, values[c], 0, count);
        }
        return (time, values);
    }
}
