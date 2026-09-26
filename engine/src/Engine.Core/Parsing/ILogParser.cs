using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Parsing;

public interface ILogParser
{
    string Id { get; }

    string DisplayName { get; }

    bool CanParse(string path, ReadOnlySpan<char> preview);

    ParsedLog Parse(string path);
}
