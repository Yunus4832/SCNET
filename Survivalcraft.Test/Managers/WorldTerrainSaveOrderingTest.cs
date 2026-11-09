using Engine.Core.FileStorage;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldTerrainSaveOrderingTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MetadataWaitsForTerrainAndFailuresDoNotCommitIt(bool writeFailure, bool captureFailure)
    {
        const string rootName = "terrain-save-order-test";
        var directory = Path.Combine(Path.GetTempPath(), "scnet-save-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Storage.RegisterFileSystemRoot(rootName, directory);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var failWrites = writeFailure;
        try
        {
            using var world = new WorldCatalogScope(RuntimeSide.Authority);
            using var coordinator = new TerrainSaveCoordinator((_, _, _) =>
                {
                    started.Set();
                    release.Wait();
                    if (failWrites) { throw new IOException("simulated terrain failure"); }
                }
            );
            var terrain = new SubsystemTerrain { Terrain = new Terrain() };
            world.Project.AddSubsystem(
                new SubsystemGameInfo
                {
                    WorldSettings = new WorldSettings { Name = "Save ordering" },
                    DirectoryName = rootName + ":world"
                }
            );
            world.Project.AddSubsystem(terrain);
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.TerrainSaveCoordinator))!
                .SetValue(terrain, coordinator);
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
                .SetValue(terrain, new TerrainCellAuthority(terrain.Terrain));
            var chunk = terrain.Terrain.AllocateChunk(0, 0);
            chunk.HasContents = true;
            chunk.Cells[0] = 7;
            chunk.ModificationCounter = 1;
            if (captureFailure) { world.Project.AddSubsystem(new CaptureFailure()); }

            using var persistence = new WorldProjectPersistence(world.Project);
            var outcome =
                new TaskCompletionSource<WorldSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            persistence.SaveCompleted += result => outcome.TrySetResult(result);
            try
            {
                var accepted = Task.Run(() => persistence.Save(false));
                var operation = await accepted.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(outcome.Task.IsCompleted);
                Assert.False(File.Exists(Path.Combine(directory, "world", "Project.xml")));
                release.Set();
                var result = await outcome.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(operation, result.OperationId);
                Assert.Equal(writeFailure || captureFailure, result.Failure != null);
                Assert.Equal(
                    !writeFailure && !captureFailure,
                    File.Exists(Path.Combine(directory, "world", "Project.xml"))
                );
                Assert.False(world.Project.IsDisposed);
                Assert.Equal(0, coordinator.OutstandingCount);
            }
            finally
            {
                failWrites = false;
                release.Set();
            }
        }
        finally
        {
            release.Set();
            Storage.RegisterFileSystemRoot(rootName, AppContext.BaseDirectory);
            Directory.Delete(directory, true);
        }
    }

    private sealed class CaptureFailure : Subsystem
    {
        public override void Save(ValuesDictionary valuesDictionary)
        {
            throw new IOException("simulated capture failure");
        }
    }
}
