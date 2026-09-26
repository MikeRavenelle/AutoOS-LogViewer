using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class GenericCsvParserTests
{
    private static string TempCsv(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"olv-generic-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CanParse_PlainCommaCsvWithTimeFirst()
    {
        var parser = new GenericCsvParser();
        Assert.True(parser.CanParse("x.csv", "Time,RPM,MAP\n0.0,800,101\n0.1,850,102\n"));
    }

    [Fact]
    public void CanParse_RejectsFileWithNoTimeColumn()
    {
        var parser = new GenericCsvParser();
        Assert.False(parser.CanParse("x.csv", "RPM,MAP,TPS\n800,101,10\n850,102,11\n"));
    }

    [Fact]
    public void CanParse_RejectsFreeTextFile()
    {
        var parser = new GenericCsvParser();
        Assert.False(parser.CanParse("x.csv", "This is just a text file.\nNo tabular data here at all.\n"));
    }

    [Fact]
    public void Parse_CommaDelimited_ProducesChannelsAndTime()
    {
        var path = TempCsv("Time,RPM,MAP (kPa)\n0.0,800,101.3\n0.1,850,102.5\n0.2,900,103.1\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal(3, log.SampleCount);
        Assert.Equal(2, log.Channels.Length);
        Assert.Equal("RPM", log.Channels[0].Name);
        Assert.Equal("MAP", log.Channels[1].Name);
        Assert.Equal("kPa", log.Channels[1].Unit);
        Assert.Equal([0.0, 0.1, 0.2], log.Time);
        Assert.Equal(800f, log.Data[0][0]);
        Assert.Equal(103.1f, log.Data[1][2]);
    }

    [Fact]
    public void Parse_SemicolonDelimited()
    {
        var path = TempCsv("TIME;RPM;BOOST\n0.0;800;0.5\n0.5;1200;5.2\n1.0;1500;12.8\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal(3, log.SampleCount);
        Assert.Equal(["RPM", "BOOST"], log.Channels.Select(c => c.Name));
        Assert.Equal(12.8f, log.Data[1][2]);
    }

    [Fact]
    public void Parse_TabDelimited()
    {
        var path = TempCsv("Time\tRPM\tTPS\n0.0\t800\t0\n0.1\t1000\t20\n0.2\t1200\t45\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal(3, log.SampleCount);
        Assert.Equal(1200f, log.Data[0][2]);
    }

    [Fact]
    public void Parse_SkipsLeadingMetadataLines()
    {
        var path = TempCsv(
            "Log started 2026-07-18\n" +
            "Vehicle: Test Car\n" +
            "Time,RPM,MAP\n" +
            "0.0,800,101\n" +
            "0.1,850,102\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal(2, log.SampleCount);
        Assert.Equal(2, log.Channels.Length);
    }

    [Fact]
    public void Parse_HhMmSsTimeFormat_ConvertsToSeconds()
    {
        var path = TempCsv("Log Time,RPM\n00:00:00.000,800\n00:00:01.500,1200\n00:01:02.250,900\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal(0.0, log.Time[0]);
        Assert.Equal(1.5, log.Time[1], 3);
        Assert.Equal(62.25, log.Time[2], 3);
    }

    [Fact]
    public void Parse_UnparseableCell_BecomesNaN()
    {
        var path = TempCsv("Time,RPM\n0.0,800\n0.1,N/A\n0.2,900\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.True(float.IsNaN(log.Data[0][1]));
    }

    [Fact]
    public void Parse_QuotedFieldWithEmbeddedDelimiter()
    {
        var path = TempCsv("Time,\"Boost Error, RaceROM (psi)\",RPM\n0.0,-3.5,800\n0.1,-4.1,850\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal(2, log.Channels.Length);
        Assert.Equal("Boost Error, RaceROM", log.Channels[0].Name);
        Assert.Equal("psi", log.Channels[0].Unit);
        Assert.Equal(-3.5f, log.Data[0][0]);
    }

    [Fact]
    public void Parse_QuotedFieldWithEscapedQuote()
    {
        var path = TempCsv("Time,\"Sensor \"\"A\"\" Value\",RPM\n0.0,1.5,800\n0.1,1.6,850\n");
        var log = new GenericCsvParser().Parse(path);

        Assert.Equal("Sensor \"A\" Value", log.Channels[0].Name);
    }

    [Fact]
    public void Registry_FallsBackToGenericForUnrecognizedCsv()
    {
        var path = TempCsv("Time,RPM,MAP\n0.0,800,101\n0.1,850,102\n");
        var registry = ParserRegistry.CreateDefault();
        var parser = registry.ResolveFor(path);

        Assert.NotNull(parser);
        Assert.Equal("generic-csv", parser!.Id);
    }
}
