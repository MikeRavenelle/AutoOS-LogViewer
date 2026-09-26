using System.Globalization;
using System.Text;
using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Series;

public static class CsvExport
{
    public static string Build(ParsedLog log, int[] channelIds, double t0, double t1)
    {
        var sb = new StringBuilder();
        sb.Append("Time (s)");
        foreach (int id in channelIds)
        {
            var c = log.Channels[id];
            sb.Append(',').Append(CsvField(c.Unit.Length > 0 ? $"{c.Name} ({c.Unit})" : c.Name));
        }
        sb.Append('\n');

        var (time, values) = RawSeries.Slice(log, channelIds, t0, t1);
        for (int i = 0; i < time.Length; i++)
        {
            sb.Append(time[i].ToString(CultureInfo.InvariantCulture));
            foreach (var col in values)
            {
                float v = col[i];
                sb.Append(',');
                if (!float.IsNaN(v))
                    sb.Append(v.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string CsvField(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;
}
