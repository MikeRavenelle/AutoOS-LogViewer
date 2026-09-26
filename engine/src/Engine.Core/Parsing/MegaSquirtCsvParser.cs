using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

public sealed class MegaSquirtCsvParser : ILogParser
{
    public string Id => "megasquirt-csv";
    public string DisplayName => "MegaSquirt / TunerStudio CSV";

    private static readonly string[] Signatures = ["secl", "gammae"];

    public bool CanParse(string path, ReadOnlySpan<char> preview)
    {
        string text = preview.ToString();
        return HasSignature(text) && DelimitedCsvCore.Detect(text) is not null;
    }

    public ParsedLog Parse(string path) => DelimitedCsvCore.Parse(path, Id);

    private static bool HasSignature(string text) =>
        Signatures.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));
}
