using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Terrains;

public sealed class ClientTerrainPersistenceTest
{
    [Fact]
    public void ExportingAuthorityBootstrapDoesNotQueueDiskWrites()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.SendToClientMode = true;
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        terrain.Save(new ValuesDictionary());
        Assert.True(terrain.PendingTerrainSave.IsCompletedSuccessfully);
        Assert.Null(terrain.TerrainSaveCoordinator);
    }

    [Fact]
    public void SavingAReplicaDoesNotRequireStorageOrTerrainWorkers()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);

        terrain.Save(new ValuesDictionary());

        Assert.Null(terrain.TerrainSerializer);
        Assert.Null(terrain.TerrainSaveCoordinator);
    }

    [Fact]
    public void SavingAuthorityWithoutItsSaveCoordinatorCannotSucceedSilently()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);

        var exception = Assert.Throws<InvalidOperationException>(() => terrain.Save(new ValuesDictionary()));

        Assert.Contains("save coordinator", exception.Message);
        Assert.False(project.IsDisposed);
    }
}
