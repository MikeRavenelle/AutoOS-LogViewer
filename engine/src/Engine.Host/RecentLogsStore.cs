using System.Text.Json;

namespace OpenLogViewer.Engine.Host;

public sealed record RecentLogEntry(string Path, string OpenedAt);

public sealed class RecentLogsStore(string path)
{
    private const int MaxEntries = 10;
    private readonly Lock _lock = new();

    public static RecentLogsStore CreateDefault()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLogViewer");
        return new RecentLogsStore(Path.Combine(dir, "recent-logs.json"));
    }

    public List<RecentLogEntry> List()
    {
        lock (_lock)
        {
            return Load();
        }
    }

    public List<RecentLogEntry> Touch(string logPath)
    {
        lock (_lock)
        {
            var entries = Load();
            entries.RemoveAll(e => e.Path == logPath);
            entries.Insert(0, new RecentLogEntry(logPath, DateTimeOffset.UtcNow.ToString("O")));
            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            Persist(entries);
            return entries;
        }
    }

    public List<RecentLogEntry> Remove(string logPath)
    {
        lock (_lock)
        {
            var entries = Load();
            entries.RemoveAll(e => e.Path == logPath);
            Persist(entries);
            return entries;
        }
    }

    private List<RecentLogEntry> Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<RecentLogEntry>>(
                File.ReadAllText(path), CommandDispatcher.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Persist(List<RecentLogEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(entries, CommandDispatcher.JsonOptions));
    }
}
