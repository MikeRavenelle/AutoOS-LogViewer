using OpenLogViewer.Engine.Parsing;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class PlatformCsvParserTests
{
    private static string TempCsv(string content, string ext = "csv")
    {
        string path = Path.Combine(Path.GetTempPath(), $"olv-platform-{Guid.NewGuid():N}.{ext}");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void MegaSquirt_DetectedBySecLColumn()
    {
        var parser = new MegaSquirtCsvParser();
        string preview = "Time,SecL,RPM,MAP,TPS,AFR,GammaE\n0.0,0,800,101,0,14.7,100\n0.1,10,850,102,0,14.6,101\n";
        Assert.True(parser.CanParse("x.csv", preview));

        var path = TempCsv(preview);
        var log = parser.Parse(path);
        Assert.Equal("megasquirt-csv", log.ParserId);
        Assert.Contains(log.Channels, c => c.Name == "RPM");
    }

    [Fact]
    public void MegaSquirt_DoesNotClaimUnrelatedCsv()
    {
        var parser = new MegaSquirtCsvParser();
        Assert.False(parser.CanParse("x.csv", "Time,RPM,MAP\n0.0,800,101\n0.1,850,102\n"));
    }

    [Fact]
    public void RomRaider_DetectedBySubaruSpecificColumns()
    {
        var parser = new RomRaiderCsvParser();
        string preview =
            "Time,RPM,A/F Correction #1,A/F Learning #1,Feedback Knock Correction\n" +
            "0.0,800,0.5,1.2,0\n0.1,850,0.6,1.1,0\n";
        Assert.True(parser.CanParse("x.csv", preview));

        var path = TempCsv(preview);
        var log = parser.Parse(path);
        Assert.Equal("romraider-csv", log.ParserId);
    }

    [Fact]
    public void RomRaider_RequiresAtLeastTwoSignatureColumns()
    {
        var parser = new RomRaiderCsvParser();
        Assert.False(parser.CanParse("x.csv", "Time,RPM,A/F Correction #1\n0.0,800,0.5\n0.1,850,0.6\n"));
    }

    [Fact]
    public void Registry_PrefersMegaSquirtOverGenericWhenSignaturePresent()
    {
        var path = TempCsv("Time,SecL,RPM,MAP\n0.0,0,800,101\n0.1,10,850,102\n");
        var registry = ParserRegistry.CreateDefault();
        var parser = registry.ResolveFor(path);

        Assert.NotNull(parser);
        Assert.Equal("megasquirt-csv", parser!.Id);
    }

    [Fact]
    public void Registry_PrefersRomRaiderOverGenericWhenSignaturesPresent()
    {
        var path = TempCsv(
            "Time,RPM,A/F Correction #1,A/F Learning #1\n0.0,800,0.5,1.2\n0.1,850,0.6,1.1\n");
        var registry = ParserRegistry.CreateDefault();
        var parser = registry.ResolveFor(path);

        Assert.NotNull(parser);
        Assert.Equal("romraider-csv", parser!.Id);
    }
}
