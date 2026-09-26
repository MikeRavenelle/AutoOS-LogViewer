using OpenLogViewer.Engine.Host;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class RecentLogsStoreTests
{
    private static RecentLogsStore NewStore()
        => new(Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json"));

    [Fact]
    public void List_BeforeAnyTouch_ReturnsEmpty()
    {
        Assert.Empty(NewStore().List());
    }

    [Fact]
    public void Touch_AddsMostRecentFirst()
    {
        var store = NewStore();
        store.Touch("/a.csv");
        var entries = store.Touch("/b.csv");

        Assert.Equal(["/b.csv", "/a.csv"], entries.Select(e => e.Path));
    }

    [Fact]
    public void Touch_ReopeningExistingPath_MovesItToFront_DoesNotDuplicate()
    {
        var store = NewStore();
        store.Touch("/a.csv");
        store.Touch("/b.csv");
        var entries = store.Touch("/a.csv");

        Assert.Equal(["/a.csv", "/b.csv"], entries.Select(e => e.Path));
    }

    [Fact]
    public void Touch_BeyondMaxEntries_DropsOldest()
    {
        var store = NewStore();
        for (int i = 0; i < 11; i++) store.Touch($"/log{i}.csv");
        var entries = store.Touch("/log11.csv");

        Assert.Equal(10, entries.Count);
        Assert.Equal("/log11.csv", entries[0].Path);
        Assert.DoesNotContain(entries, e => e.Path == "/log0.csv");
    }

    [Fact]
    public void Remove_DropsOnlyThatPath()
    {
        var store = NewStore();
        store.Touch("/a.csv");
        store.Touch("/b.csv");

        var entries = store.Remove("/a.csv");

        Assert.Single(entries);
        Assert.Equal("/b.csv", entries[0].Path);
    }

    [Fact]
    public void PersistsAcrossInstances_SameBackingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"olv-recent-{Guid.NewGuid():N}.json");
        new RecentLogsStore(path).Touch("/a.csv");

        var reloaded = new RecentLogsStore(path).List();

        Assert.Equal("/a.csv", reloaded[0].Path);
    }
}
