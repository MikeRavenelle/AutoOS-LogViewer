using OpenLogViewer.Engine.Model;

namespace OpenLogViewer.Engine.Series;

public static class LogMarks
{
    public static double[] Find(ParsedLog log)
    {
        var channel = log.Channels.FirstOrDefault(c => c.Name == "Log Mark");
        if (channel is null) return [];

        var values = log.Data[channel.Id];
        var times = new List<double>();
        for (int i = 0; i < values.Length; i++)
        {
            if (!float.IsNaN(values[i]) && values[i] != 0)
                times.Add(log.Time[i]);
        }
        return [.. times];
    }
}
