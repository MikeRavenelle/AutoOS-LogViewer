using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Host.Protocol;
using Xunit;

namespace OpenLogViewer.Engine.Tests;

public class LayoutStoreTests
{
    private static LayoutStore NewStore(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"olv-layouts-{Guid.NewGuid():N}.json");
        return new LayoutStore(path);
    }

    [Fact]
    public void SaveListDelete_RoundTripsThroughDisk()
    {
        var store = NewStore(out var path);
        try
        {
            Assert.Empty(store.List());

            store.Save(new LayoutDto("Boost health",
                [new LayoutPaneDto(["Boost Pressure Actual Sensor 1 (psi)", "Boost Pressure Target Sensor 1 (psi)"]),
                 new LayoutPaneDto(["Boost Error (RaceROM) (psi)"])]));
            store.Save(new LayoutDto("Fueling", [new LayoutPaneDto(["Lambda Actual B1S1 (lambda)"])]));

            var reloaded = new LayoutStore(path).List();
            Assert.Equal(["Boost health", "Fueling"], reloaded.Select(l => l.Name));
            Assert.Equal(2, reloaded[0].Panes.Length);

            store.Save(new LayoutDto("Fueling", [new LayoutPaneDto(["Fuel Rail Pressure (HPFP) (psi)"])]));
            var afterUpsert = store.List();
            Assert.Equal(2, afterUpsert.Length);
            Assert.Equal("Fuel Rail Pressure (HPFP) (psi)",
                afterUpsert.Single(l => l.Name == "Fueling").Panes[0].ChannelLabels[0]);

            var afterDelete = store.Delete("Boost health");
            Assert.Equal(["Fueling"], afterDelete.Select(l => l.Name));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CorruptFile_StartsFreshInsteadOfCrashing()
    {
        var store = NewStore(out var path);
        try
        {
            File.WriteAllText(path, "{not json");
            Assert.Empty(store.List());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
