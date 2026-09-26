using System.Collections.Concurrent;
using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Host;

public sealed class LogStore
{
    private readonly ConcurrentDictionary<int, ParsedLog> _logs = new();
    private int _nextId;

    public (int Id, ParsedLog Log) Add(ParsedLog log)
    {
        int id = Interlocked.Increment(ref _nextId);
        _logs[id] = log;
        return (id, log);
    }

    public ParsedLog? Get(int id) => _logs.GetValueOrDefault(id);

    public bool Remove(int id) => _logs.TryRemove(id, out _);
}
