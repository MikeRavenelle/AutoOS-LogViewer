using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

public sealed class RomRaiderCsvParser : ILogParser
{
    public string Id => "romraider-csv";
    public string DisplayName => "RomRaider CSV";

    private static readonly string[] Signatures =
    [
        "a/f learning", "a/f correction", "feedback knock correction",
        "fine learning knock correction", "manifold relative pressure",
    ];

    public bool CanParse(string path, ReadOnlySpan<char> preview)
    {
        string text = preview.ToString();
        int hits = Signatures.Count(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));
        return hits >= 2 && DelimitedCsvCore.Detect(text) is not null;
    }

    public ParsedLog Parse(string path) => DelimitedCsvCore.Parse(path, Id);
}
