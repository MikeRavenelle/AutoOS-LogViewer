namespace OpenLogViewer.Engine.Parsing;

public sealed class ParserRegistry
{
    private readonly List<ILogParser> _parsers = [];

    public IReadOnlyList<ILogParser> Parsers => _parsers;

    public void Register(ILogParser parser) => _parsers.Add(parser);

    public ILogParser? ResolveFor(string path)
    {
        const int previewChars = 4096;
        using var reader = new StreamReader(path);
        var buffer = new char[previewChars];
        int read = reader.ReadBlock(buffer, 0, previewChars);
        var preview = buffer.AsSpan(0, read);

        foreach (var parser in _parsers)
        {
            if (parser.CanParse(path, preview))
                return parser;
        }
        return null;
    }

    public static ParserRegistry CreateDefault()
    {
        var registry = new ParserRegistry();
        registry.Register(new EcuConnectCsvParser());
        registry.Register(new MegaLogViewerMlgParser());
        registry.Register(new MegaSquirtCsvParser());
        registry.Register(new RomRaiderCsvParser());
        registry.Register(new GenericCsvParser());
        return registry;
    }
}
