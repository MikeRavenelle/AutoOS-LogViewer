using OpenLogViewer.Engine.Model;
using OpenLogViewer.Engine.Series;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class CsvExportTests
{
    private static ParsedLog NewLog(double[] time, float[] a, float[] b) => new()
    {
        SourcePath = "test.csv",
        ParserId = "test",
        Metadata = new Dictionary<string, string>(),
        Time = time,
        Channels =
        [
            new ChannelInfo(0, "Boost Pressure Actual Sensor 1", "psi"),
            new ChannelInfo(1, "Log Mark", ""),
        ],
        Data = [a, b],
    };

    [Fact]
    public void BasicRange_EmitsHeaderAndRows()
    {
        var log = NewLog([0, 1, 2, 3], [10, 20, 30, 40], [0, 0, 1, 0]);

        var csv = CsvExport.Build(log, [0, 1], 1.0, 2.0);
        var lines = csv.TrimEnd('\n').Split('\n');

        Assert.Equal("Time (s),Boost Pressure Actual Sensor 1 (psi),Log Mark", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Equal("1,20,0", lines[1]);
        Assert.Equal("2,30,1", lines[2]);
    }

    [Fact]
    public void NaNSample_EmitsEmptyField()
    {
        var log = NewLog([0, 1], [10, float.NaN], [0, 0]);

        var csv = CsvExport.Build(log, [0], 0, 1);
        var lines = csv.TrimEnd('\n').Split('\n');

        Assert.Equal("1,", lines[2]);
    }

    [Fact]
    public void EmptyRange_EmitsHeaderOnly()
    {
        var log = NewLog([0, 1, 2], [10, 20, 30], [0, 0, 0]);

        var csv = CsvExport.Build(log, [0], 5, 6);

        Assert.Equal("Time (s),Boost Pressure Actual Sensor 1 (psi)\n", csv);
    }

    [Fact]
    public void UnitLessChannel_OmitsParens()
    {
        var log = NewLog([0], [10], [1]);

        var csv = CsvExport.Build(log, [1], 0, 0);

        Assert.StartsWith("Time (s),Log Mark\n", csv);
    }
}
