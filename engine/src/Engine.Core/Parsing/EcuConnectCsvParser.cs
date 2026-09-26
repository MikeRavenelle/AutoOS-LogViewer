using System.Globalization;
using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

public sealed class EcuConnectCsvParser : ILogParser
{
    public string Id => "ecuconnect-csv";
    public string DisplayName => "ECU Connect CSV";

    public bool CanParse(string path, ReadOnlySpan<char> preview)
        => preview.StartsWith("#Encoding:") || preview.Contains("#App name: ECU Connect", StringComparison.Ordinal);

    public ParsedLog Parse(string path)
    {
        string text = File.ReadAllText(path);
        ReadOnlySpan<char> span = text.AsSpan();

        var metadata = new Dictionary<string, string>();
        string? headerLine = null;
        int dataStart = 0;

        foreach (var lineRange in span.Split('\n'))
        {
            var line = span[lineRange].TrimEnd('\r');
            if (line.IsEmpty) continue;

            if (line[0] == '#')
            {
                int colon = line.IndexOf(':');
                if (colon > 1)
                    metadata[line[1..colon].Trim().ToString()] = line[(colon + 1)..].Trim().ToString();
            }
            else
            {
                headerLine = line.ToString();
                dataStart = lineRange.End.GetOffset(span.Length) + 1;
                break;
            }
        }

        if (headerLine is null)
            throw new FormatException("No CSV header row found after metadata block.");

        string[] headers = headerLine.Split(',');
        if (headers.Length < 2 || !headers[0].StartsWith("Time", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Expected first column 'Time (s)', got '{headers[0]}'.");

        int channelCount = headers.Length - 1;
        var channels = new ChannelInfo[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            var (name, unit) = ChannelNaming.SplitNameUnit(headers[i + 1]);
            channels[i] = new ChannelInfo(i, name, unit);
        }

        var dataSpan = span[Math.Min(dataStart, span.Length)..];
        int rowCount = 0;
        foreach (var lineRange in dataSpan.Split('\n'))
        {
            if (!dataSpan[lineRange].TrimEnd('\r').IsEmpty) rowCount++;
        }

        var time = new double[rowCount];
        var data = new float[channelCount][];
        for (int c = 0; c < channelCount; c++)
            data[c] = new float[rowCount];

        int row = 0;
        foreach (var lineRange in dataSpan.Split('\n'))
        {
            var line = dataSpan[lineRange].TrimEnd('\r');
            if (line.IsEmpty) continue;

            int col = 0;
            foreach (var cellRange in line.Split(','))
            {
                var cell = line[cellRange].Trim();
                if (col == 0)
                {
                    time[row] = double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
                        ? t : double.NaN;
                }
                else if (col <= channelCount)
                {
                    data[col - 1][row] = float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                        ? v : float.NaN;
                }
                col++;
            }
            row++;
        }

        return new ParsedLog
        {
            SourcePath = path,
            ParserId = Id,
            Metadata = metadata,
            Time = time,
            Channels = channels,
            Data = data,
        };
    }
}
