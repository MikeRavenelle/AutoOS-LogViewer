using System.Text.Json;
using OpenLogViewer.Engine.Host.Protocol;

namespace OpenLogViewer.Engine.Host;

public sealed class LayoutStore(string path)
{
    private readonly Lock _lock = new();

    public static LayoutStore CreateDefault()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLogViewer");
        return new LayoutStore(Path.Combine(dir, "layouts.json"));
    }

    public LayoutDto[] List()
    {
        lock (_lock)
        {
            return Load();
        }
    }

    public LayoutDto[] Save(LayoutDto layout)
    {
        lock (_lock)
        {
            var layouts = Load().Where(l => l.Name != layout.Name).Append(layout)
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            Persist(layouts);
            return layouts;
        }
    }

    public LayoutDto[] Delete(string name)
    {
        lock (_lock)
        {
            var layouts = Load().Where(l => l.Name != name).ToArray();
            Persist(layouts);
            return layouts;
        }
    }

    private LayoutDto[] Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<LayoutDto[]>(
                File.ReadAllText(path), CommandDispatcher.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Persist(LayoutDto[] layouts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(layouts, CommandDispatcher.JsonOptions));
    }
}
