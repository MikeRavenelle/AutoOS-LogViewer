using OpenLogViewer.Engine.Host;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class AnnotationStoreTests
{
    private static AnnotationStore NewStore()
        => new(Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json"));

    [Fact]
    public void Add_ThenList_ReturnsIt()
    {
        var store = NewStore();
        store.Add("log.csv", 3.5, "wastegate rattle starts");

        var list = store.List("log.csv");

        Assert.Single(list);
        Assert.Equal(3.5, list[0].Time);
        Assert.Equal("wastegate rattle starts", list[0].Text);
    }

    [Fact]
    public void List_UnknownSourcePath_ReturnsEmpty()
    {
        var store = NewStore();

        Assert.Empty(store.List("never-opened.csv"));
    }

    [Fact]
    public void Add_SortsByTime()
    {
        var store = NewStore();
        store.Add("log.csv", 5.0, "second");
        store.Add("log.csv", 1.0, "first");

        var list = store.List("log.csv");

        Assert.Equal(["first", "second"], list.Select(a => a.Text));
    }

    [Fact]
    public void Delete_RemovesOnlyThatAnnotation()
    {
        var store = NewStore();
        store.Add("log.csv", 1.0, "keep");
        var toDelete = store.Add("log.csv", 2.0, "remove")[^1];

        var list = store.Delete("log.csv", toDelete.Id);

        Assert.Single(list);
        Assert.Equal("keep", list[0].Text);
    }

    [Fact]
    public void PersistsAcrossInstances_SameBackingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"olv-annotations-{Guid.NewGuid():N}.json");
        new AnnotationStore(path).Add("log.csv", 1.0, "note");

        var reloaded = new AnnotationStore(path).List("log.csv");

        Assert.Single(reloaded);
        Assert.Equal("note", reloaded[0].Text);
    }

    [Fact]
    public void DifferentSourcePaths_AreIsolated()
    {
        var store = NewStore();
        store.Add("a.csv", 1.0, "for a");
        store.Add("b.csv", 1.0, "for b");

        Assert.Single(store.List("a.csv"));
        Assert.Single(store.List("b.csv"));
        Assert.Equal("for a", store.List("a.csv")[0].Text);
    }
}
