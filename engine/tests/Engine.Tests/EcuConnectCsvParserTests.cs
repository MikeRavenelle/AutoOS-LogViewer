using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class EcuConnectCsvParserTests
{
    private static string FixturePath(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "fixtures", name);
    }

    private const string Leaking = "2026-07-11_run1_leaking.csv";

    [Fact]
    public void CanParse_RecognizesEcuConnectPreview()
    {
        var parser = new EcuConnectCsvParser();
        Assert.True(parser.CanParse("x.csv", "#Encoding: UTF-8\n#App name: ECU Connect\n"));
        Assert.False(parser.CanParse("x.csv", "Time,RPM\n0,800\n"));
    }

    [Fact]
    public void Registry_ResolvesFixtureToEcuConnectParser()
    {
        var registry = ParserRegistry.CreateDefault();
        var parser = registry.ResolveFor(FixturePath(Leaking));
        Assert.NotNull(parser);
        Assert.Equal("ecuconnect-csv", parser!.Id);
    }

    [Fact]
    public void Parse_ReadsMetadataBlock()
    {
        var log = new EcuConnectCsvParser().Parse(FixturePath(Leaking));
        Assert.Equal("ECU Connect", log.Metadata["App name"]);
        Assert.Equal("Toyota G16E-GTS", log.Metadata["Vehicle"]);
        Assert.Equal("91oct Rev3", log.Metadata["Ecu programming comment"]);
    }

    [Fact]
    public void Parse_ProducesStructOfArraysWithConsistentLengths()
    {
        var log = new EcuConnectCsvParser().Parse(FixturePath(Leaking));

        Assert.Equal(120, log.SampleCount);
        Assert.Equal(69, log.Channels.Length);
        Assert.Equal(log.Channels.Length, log.Data.Length);
        Assert.All(log.Data, column => Assert.Equal(log.SampleCount, column.Length));

        Assert.Equal(0.0, log.Time[0]);
        Assert.True(log.EndTime > log.StartTime);
        for (int i = 1; i < log.Time.Length; i++)
            Assert.True(log.Time[i] > log.Time[i - 1]);
    }

    [Fact]
    public void Parse_ReadsExactSampleValues()
    {
        var log = new EcuConnectCsvParser().Parse(FixturePath(Leaking));

        Assert.Equal("Accelerator Position", log.Channels[1].Name);
        Assert.Equal(42.0f, log.Data[1][0]);
        Assert.Equal(14.21f, log.Data[2][0]);
        Assert.Equal(-3.5660f, log.Data[3][0], 4);
    }

    [Theory]
    [InlineData("Time (s)", "Time", "s")]
    [InlineData("Boost Error (RaceROM) (psi)", "Boost Error (RaceROM)", "psi")]
    [InlineData("Boost Target Max Reason (RaceROM)", "Boost Target Max Reason (RaceROM)", "")]
    [InlineData("Log Mark", "Log Mark", "")]
    [InlineData("Coolant Temperature (°F)", "Coolant Temperature", "°F")]
    [InlineData("Fuel Trim Long Term Sensor 1 (%FT)", "Fuel Trim Long Term Sensor 1", "%FT")]
    [InlineData("Torque Request (Wide Open Pedal) (Nm)", "Torque Request (Wide Open Pedal)", "Nm")]
    public void SplitNameUnit_SeparatesUnitsFromQualifiers(string header, string name, string unit)
    {
        var (parsedName, parsedUnit) = ChannelNaming.SplitNameUnit(header);
        Assert.Equal(name, parsedName);
        Assert.Equal(unit, parsedUnit);
    }

    [Fact]
    public void Parse_HandlesDuplicateChannelNamesById()
    {
        var log = new EcuConnectCsvParser().Parse(FixturePath(Leaking));
        var desired = log.Channels.Where(c => c.Name == "Engine Load Desired").ToArray();
        Assert.Equal(2, desired.Length);
        Assert.NotEqual(desired[0].Id, desired[1].Id);
    }

    [Fact]
    public void Parse_AllFixturesParseCleanly()
    {
        var parser = new EcuConnectCsvParser();
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(FixturePath(Leaking))!, "*.csv"))
        {
            var log = parser.Parse(file);
            Assert.True(log.SampleCount > 100, $"{file}: {log.SampleCount} samples");
            Assert.InRange(log.Channels.Length, 60, 80);
            Assert.All(log.Data, column => Assert.Equal(log.SampleCount, column.Length));
        }
    }
}
