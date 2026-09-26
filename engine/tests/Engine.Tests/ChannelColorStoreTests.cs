using OpenLogViewer.Engine.Host;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class ChannelColorStoreTests
{
    private static ChannelColorStore NewStore()
        => new(Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json"));

    [Fact]
    public void Set_ThenList_ReturnsIt()
    {
        var store = NewStore();
        var colors = store.Set("Boost Error (RaceROM) (psi)", "#ff2d55");

        Assert.Equal("#ff2d55", colors["Boost Error (RaceROM) (psi)"]);
    }

    [Fact]
    public void List_BeforeAnySet_ReturnsEmpty()
    {
        Assert.Empty(NewStore().List());
    }

    [Fact]
    public void Set_OverwritesExistingColorForSameLabel()
    {
        var store = NewStore();
        store.Set("RPM", "#111111");
        var colors = store.Set("RPM", "#222222");

        Assert.Equal("#222222", colors["RPM"]);
        Assert.Single(colors);
    }

    [Fact]
    public void Clear_RemovesOnlyThatLabel()
    {
        var store = NewStore();
        store.Set("RPM", "#111111");
        store.Set("MAP", "#222222");

        var colors = store.Clear("RPM");

        Assert.False(colors.ContainsKey("RPM"));
        Assert.Equal("#222222", colors["MAP"]);
    }

    [Fact]
    public void PersistsAcrossInstances_SameBackingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"olv-colors-{Guid.NewGuid():N}.json");
        new ChannelColorStore(path).Set("RPM", "#4cc2ff");

        var reloaded = new ChannelColorStore(path).List();

        Assert.Equal("#4cc2ff", reloaded["RPM"]);
    }
}
