namespace OpenLogViewer.Engine.Series;

public enum HistogramAgg
{
    Mean,
    Min,
    Max,
    Count,
}

public sealed record HistogramGrid(double[] XEdges, double[] YEdges, double?[][] Cells, int[][] Counts);

public static class Histogrammer
{
    public static HistogramGrid Compute(
        float[] x, float[] y, float[] value,
        int xBins, int yBins, HistogramAgg agg, int minCount,
        SampleFilter[]? filters = null)
    {
        if (x.Length != y.Length || y.Length != value.Length)
            throw new ArgumentException("Channel lengths differ");
        xBins = Math.Clamp(xBins, 1, 256);
        yBins = Math.Clamp(yBins, 1, 256);
        minCount = Math.Max(minCount, 1);

        bool hasFilters = filters is { Length: > 0 };

        var (xMin, xMax) = Range(x, filters, hasFilters);
        var (yMin, yMax) = Range(y, filters, hasFilters);
        if (double.IsNaN(xMin) || double.IsNaN(yMin))
            return Empty(xBins, yBins, xMin, xMax, yMin, yMax);

        var xEdges = Edges(xMin, xMax, xBins);
        var yEdges = Edges(yMin, yMax, yBins);

        var counts = NewGrid<int>(yBins, xBins);
        var sums = NewGrid<double>(yBins, xBins);
        var mins = NewGrid<double>(yBins, xBins);
        var maxs = NewGrid<double>(yBins, xBins);
        foreach (var row in mins) Array.Fill(row, double.PositiveInfinity);
        foreach (var row in maxs) Array.Fill(row, double.NegativeInfinity);

        for (int i = 0; i < x.Length; i++)
        {
            if (float.IsNaN(x[i]) || float.IsNaN(y[i]) || float.IsNaN(value[i]))
                continue;
            if (hasFilters && !SampleFilters.Keep(filters!, i))
                continue;
            int xi = BinIndex(x[i], xMin, xMax, xBins);
            int yi = BinIndex(y[i], yMin, yMax, yBins);
            counts[yi][xi]++;
            sums[yi][xi] += value[i];
            if (value[i] < mins[yi][xi]) mins[yi][xi] = value[i];
            if (value[i] > maxs[yi][xi]) maxs[yi][xi] = value[i];
        }

        var cells = new double?[yBins][];
        for (int yi = 0; yi < yBins; yi++)
        {
            cells[yi] = new double?[xBins];
            for (int xi = 0; xi < xBins; xi++)
            {
                int n = counts[yi][xi];
                if (n < minCount)
                    continue;
                cells[yi][xi] = agg switch
                {
                    HistogramAgg.Mean => sums[yi][xi] / n,
                    HistogramAgg.Min => mins[yi][xi],
                    HistogramAgg.Max => maxs[yi][xi],
                    HistogramAgg.Count => n,
                    _ => null,
                };
            }
        }

        return new HistogramGrid(xEdges, yEdges, cells, counts);
    }

    private static (double Min, double Max) Range(float[] data, SampleFilter[]? filters, bool hasFilters)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        for (int i = 0; i < data.Length; i++)
        {
            float v = data[i];
            if (float.IsNaN(v)) continue;
            if (hasFilters && !SampleFilters.Keep(filters!, i)) continue;
            if (v < min) min = v;
            if (v > max) max = v;
        }
        if (double.IsPositiveInfinity(min))
            return (double.NaN, double.NaN);
        if (min == max)
            max = min + 1;
        return (min, max);
    }

    private static double[] Edges(double min, double max, int bins)
    {
        var edges = new double[bins + 1];
        double step = (max - min) / bins;
        for (int i = 0; i <= bins; i++) edges[i] = min + i * step;
        return edges;
    }

    private static int BinIndex(double v, double min, double max, int bins)
        => Math.Clamp((int)((v - min) / (max - min) * bins), 0, bins - 1);

    private static T[][] NewGrid<T>(int rows, int cols)
    {
        var grid = new T[rows][];
        for (int r = 0; r < rows; r++) grid[r] = new T[cols];
        return grid;
    }

    private static HistogramGrid Empty(int xBins, int yBins, double xMin, double xMax, double yMin, double yMax)
    {
        var cells = new double?[yBins][];
        var counts = new int[yBins][];
        for (int r = 0; r < yBins; r++)
        {
            cells[r] = new double?[xBins];
            counts[r] = new int[xBins];
        }
        return new HistogramGrid(
            Edges(double.IsNaN(xMin) ? 0 : xMin, double.IsNaN(xMax) ? 1 : xMax, xBins),
            Edges(double.IsNaN(yMin) ? 0 : yMin, double.IsNaN(yMax) ? 1 : yMax, yBins),
            cells, counts);
    }
}
