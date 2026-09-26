using System.Text.Json;
using OpenLogViewer.Engine.Host.Protocol;

namespace OpenLogViewer.Engine.Host;

public sealed class AnnotationStore(string path)
{
    private readonly Lock _lock = new();

    public static AnnotationStore CreateDefault()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLogViewer");
        return new AnnotationStore(Path.Combine(dir, "annotations.json"));
    }

    public AnnotationDto[] List(string sourcePath)
    {
        lock (_lock)
        {
            return Load().TryGetValue(sourcePath, out var list) ? [.. list] : [];
        }
    }

    public AnnotationDto[] Add(string sourcePath, double time, string text)
    {
        lock (_lock)
        {
            var all = Load();
            if (!all.TryGetValue(sourcePath, out var list))
                all[sourcePath] = list = [];
            list.Add(new AnnotationDto(Guid.NewGuid().ToString("N"), time, text));
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            Persist(all);
            return [.. list];
        }
    }

    public AnnotationDto[] Delete(string sourcePath, string id)
    {
        lock (_lock)
        {
            var all = Load();
            if (!all.TryGetValue(sourcePath, out var list))
                return [];
            list.RemoveAll(a => a.Id == id);
            Persist(all);
            return [.. list];
        }
    }

    private Dictionary<string, List<AnnotationDto>> Load()
    {
        try
        {
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<Dictionary<string, List<AnnotationDto>>>(
                File.ReadAllText(path), CommandDispatcher.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Persist(Dictionary<string, List<AnnotationDto>> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(all, CommandDispatcher.JsonOptions));
    }
}
