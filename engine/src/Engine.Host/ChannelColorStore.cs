using System.Text.Json;

namespace OpenLogViewer.Engine.Host;

public sealed class ChannelColorStore(string path)
{
    private readonly Lock _lock = new();

    public static ChannelColorStore CreateDefault()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLogViewer");
        return new ChannelColorStore(Path.Combine(dir, "channel-colors.json"));
    }

    public Dictionary<string, string> List()
    {
        lock (_lock)
        {
            return Load();
        }
    }

    public Dictionary<string, string> Set(string label, string color)
    {
        lock (_lock)
        {
            var colors = Load();
            colors[label] = color;
            Persist(colors);
            return colors;
        }
    }

    public Dictionary<string, string> Clear(string label)
    {
        lock (_lock)
        {
            var colors = Load();
            colors.Remove(label);
            Persist(colors);
            return colors;
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(path), CommandDispatcher.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Persist(Dictionary<string, string> colors)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(colors, CommandDispatcher.JsonOptions));
    }
}
