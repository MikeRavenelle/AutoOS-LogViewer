using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

public sealed class GenericCsvParser : ILogParser
{
    public string Id => "generic-csv";
    public string DisplayName => "Generic CSV (auto-detected columns)";

    public bool CanParse(string path, ReadOnlySpan<char> preview)
        => DelimitedCsvCore.Detect(preview.ToString()) is not null;

    public ParsedLog Parse(string path) => DelimitedCsvCore.Parse(path, Id);
}
