namespace OpenLogViewer.Engine.Parsing;

internal static class ChannelNaming
{
    private static readonly HashSet<string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "s", "ms", "%", "%FT", "psi", "kPa", "bar", "°F", "°C", "rpm", "gear",
        "°", "°CK", "deg", "ml", "lambda", "AFR", "g/s", "hz", "Nm", "mph",
        "km/h", "V", "A",
    };

    public static (string Name, string Unit) SplitNameUnit(string header)
    {
        header = header.Trim();
        if (header.EndsWith(')'))
        {
            int open = header.LastIndexOf('(');
            if (open > 0)
            {
                string candidate = header[(open + 1)..^1].Trim();
                if (KnownUnits.Contains(candidate))
                    return (header[..open].Trim(), candidate);
            }
        }
        return (header, "");
    }
}
