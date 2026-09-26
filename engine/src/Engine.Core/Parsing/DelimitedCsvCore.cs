using System.Globalization;
using System.Text;
using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

internal static class DelimitedCsvCore
{
    private static readonly char[] Delimiters = [',', ';', '\t'];

    internal readonly record struct Detected(int HeaderLineIndex, char Delimiter, int TimeColumn);

    public static Detected? Detect(string text)
    {
        var lines = SplitLines(text);
        foreach (char delim in Delimiters)
        {
            for (int i = 0; i < lines.Length - 1; i++)
            {
                string[] header = SplitFields(lines[i], delim);
                if (header.Length < 2) continue;

                string[] nextRow = SplitFields(lines[i + 1], delim);
                if (nextRow.Length != header.Length) continue;
                if (!nextRow.Any(f => double.TryParse(f.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                    continue;

                int timeCol = FindTimeColumn(header);
                if (timeCol < 0) continue;

                return new Detected(i, delim, timeCol);
            }
        }
        return null;
    }

    public static ParsedLog Parse(string path, string parserId)
    {
        string text = File.ReadAllText(path);
        var detected = Detect(text)
            ?? throw new FormatException("No recognizable header row with a time column found.");

        var lines = SplitLines(text);
        string[] header = SplitFields(lines[detected.HeaderLineIndex], detected.Delimiter);

        var channelCols = new List<int>();
        var channels = new List<ChannelInfo>();
        for (int col = 0; col < header.Length; col++)
        {
            if (col == detected.TimeColumn) continue;
            var (name, unit) = ChannelNaming.SplitNameUnit(header[col]);
            channels.Add(new ChannelInfo(channels.Count, name, unit));
            channelCols.Add(col);
        }

        var dataLines = lines[(detected.HeaderLineIndex + 1)..].Where(l => l.Length > 0).ToArray();
        var time = new double[dataLines.Length];
        var data = new float[channels.Count][];
        for (int c = 0; c < channels.Count; c++)
            data[c] = new float[dataLines.Length];

        for (int row = 0; row < dataLines.Length; row++)
        {
            string[] fields = SplitFields(dataLines[row], detected.Delimiter);
            time[row] = ParseTime(Field(fields, detected.TimeColumn));
            for (int c = 0; c < channelCols.Count; c++)
            {
                string cell = Field(fields, channelCols[c]);
                data[c][row] = float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? v : float.NaN;
            }
        }

        return new ParsedLog
        {
            SourcePath = path,
            ParserId = parserId,
            Metadata = new Dictionary<string, string>(),
            Time = time,
            Channels = [.. channels],
            Data = data,
        };
    }

    private static string Field(string[] fields, int index) => index < fields.Length ? fields[index].Trim() : "";

    private static string[] SplitFields(string line, char delim)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delim)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return [.. fields];
    }

    private static int FindTimeColumn(string[] header)
    {
        if (LooksLikeTime(header[0])) return 0;
        for (int i = 1; i < header.Length; i++)
            if (LooksLikeTime(header[i])) return i;
        return -1;
    }

    private static bool LooksLikeTime(string header)
    {
        var (name, _) = ChannelNaming.SplitNameUnit(header);
        return name.Contains("time", StringComparison.OrdinalIgnoreCase)
            || name.Equals("timestamp", StringComparison.OrdinalIgnoreCase);
    }

    private static double ParseTime(string cell)
    {
        if (double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return seconds;

        string[] parts = cell.Split(':');
        if (parts.Length is 2 or 3 &&
            double.TryParse(parts[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
        {
            double minutes = double.TryParse(parts[^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 0;
            double hours = parts.Length == 3 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? h : 0;
            return hours * 3600 + minutes * 60 + secs;
        }
        return double.NaN;
    }

    private static string[] SplitLines(string text) =>
        text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
}
