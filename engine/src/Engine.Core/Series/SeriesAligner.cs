namespace OpenLogViewer.Engine.Series;

public static class SeriesAligner
{
    public static (double[] T, float?[][] Columns) Align(IReadOnlyList<DecimatedSeries> series)
    {
        int n = series.Count;
        if (n == 0)
            return ([], []);
        if (n == 1)
        {
            var only = series[0];
            var column = new float?[only.V.Length];
            for (int i = 0; i < only.V.Length; i++) column[i] = only.V[i];
            return (only.T, [column]);
        }

        var cursors = new int[n];
        int capacity = 0;
        for (int s = 0; s < n; s++) capacity += series[s].T.Length;

        var t = new List<double>(capacity);
        var columns = new List<float?>[n];
        for (int s = 0; s < n; s++) columns[s] = new List<float?>(capacity);

        while (true)
        {
            double next = double.PositiveInfinity;
            for (int s = 0; s < n; s++)
            {
                if (cursors[s] < series[s].T.Length && series[s].T[cursors[s]] < next)
                    next = series[s].T[cursors[s]];
            }
            if (double.IsPositiveInfinity(next))
                break;

            t.Add(next);
            for (int s = 0; s < n; s++)
            {
                if (cursors[s] < series[s].T.Length && series[s].T[cursors[s]] == next)
                {
                    columns[s].Add(series[s].V[cursors[s]]);
                    cursors[s]++;
                }
                else
                {
                    columns[s].Add(null);
                }
            }
        }

        var outColumns = new float?[n][];
        for (int s = 0; s < n; s++) outColumns[s] = [.. columns[s]];
        return ([.. t], outColumns);
    }
}
