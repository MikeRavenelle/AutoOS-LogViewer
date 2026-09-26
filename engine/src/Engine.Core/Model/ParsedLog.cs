namespace OpenLogViewer.Engine.Model;

public sealed record ChannelInfo(int Id, string Name, string Unit, bool Computed = false);

public sealed class ParsedLog
{
    public required string SourcePath { get; init; }
    public required string ParserId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
    public required double[] Time { get; init; }
    public required ChannelInfo[] Channels { get; set; }

    public required float[][] Data { get; set; }

    public ChannelInfo AddComputedChannel(string name, string unit, float[] values)
    {
        if (values.Length != SampleCount)
            throw new ArgumentException($"Computed channel has {values.Length} samples, log has {SampleCount}.");
        var info = new ChannelInfo(Channels.Length, name, unit, Computed: true);
        Channels = [.. Channels, info];
        Data = [.. Data, values];
        return info;
    }

    public int SampleCount => Time.Length;
    public double StartTime => Time.Length > 0 ? Time[0] : 0;
    public double EndTime => Time.Length > 0 ? Time[^1] : 0;
}
