using OpenLogViewer.Engine.Host;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class CustomMathChannelStoreTests
{
    private static CustomMathChannelStore NewStore()
        => new(Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json"));

    [Fact]
    public void List_BeforeAnySave_ReturnsEmpty()
    {
        Assert.Empty(NewStore().List());
    }

    [Fact]
    public void Save_ThenList_ReturnsIt()
    {
        var store = NewStore();
        var channels = store.Save(new CustomMathChannel("Boost Lag", "psi", "[A] - [A]@-0.5s"));

        Assert.Single(channels);
        Assert.Equal("Boost Lag", channels[0].Name);
        Assert.Equal("psi", channels[0].Unit);
    }

    [Fact]
    public void Save_SameNameTwice_ReplacesRatherThanDuplicates()
    {
        var store = NewStore();
        store.Save(new CustomMathChannel("Foo", "psi", "[A]"));
        var channels = store.Save(new CustomMathChannel("Foo", "kPa", "[B]"));

        Assert.Single(channels);
        Assert.Equal("kPa", channels[0].Unit);
        Assert.Equal("[B]", channels[0].Expression);
    }

    [Fact]
    public void Delete_RemovesOnlyThatChannel()
    {
        var store = NewStore();
        store.Save(new CustomMathChannel("Foo", "psi", "[A]"));
        store.Save(new CustomMathChannel("Bar", "psi", "[B]"));

        var channels = store.Delete("Foo");

        Assert.Single(channels);
        Assert.Equal("Bar", channels[0].Name);
    }

    [Fact]
    public void PersistsAcrossInstances_SameBackingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"olv-mathchannels-{Guid.NewGuid():N}.json");
        new CustomMathChannelStore(path).Save(new CustomMathChannel("Foo", "psi", "[A]"));

        var reloaded = new CustomMathChannelStore(path).List();

        Assert.Single(reloaded);
        Assert.Equal("Foo", reloaded[0].Name);
    }
}
