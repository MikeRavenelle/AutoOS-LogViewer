using System.Text.Json;

namespace OpenLogViewer.Engine.Host;

public sealed record CustomMathChannel(string Name, string Unit, string Expression);

public sealed class CustomMathChannelStore(string path)
{
    private readonly Lock _lock = new();

    public static CustomMathChannelStore CreateDefault()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLogViewer");
        return new CustomMathChannelStore(Path.Combine(dir, "custom-math-channels.json"));
    }

    public List<CustomMathChannel> List()
    {
        lock (_lock)
        {
            return Load();
        }
    }

    public List<CustomMathChannel> Save(CustomMathChannel channel)
    {
        lock (_lock)
        {
            var channels = Load();
            channels.RemoveAll(c => c.Name == channel.Name);
            channels.Add(channel);
            Persist(channels);
            return channels;
        }
    }

    public List<CustomMathChannel> Delete(string name)
    {
        lock (_lock)
        {
            var channels = Load();
            channels.RemoveAll(c => c.Name == name);
            Persist(channels);
            return channels;
        }
    }

    private List<CustomMathChannel> Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<CustomMathChannel>>(
                File.ReadAllText(path), CommandDispatcher.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Persist(List<CustomMathChannel> channels)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(channels, CommandDispatcher.JsonOptions));
    }
}
