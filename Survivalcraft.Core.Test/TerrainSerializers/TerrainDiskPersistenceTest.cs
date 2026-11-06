using Engine.Core.FileStorage;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.TerrainSerializers;

public sealed class TerrainDiskPersistenceTest
{
    [Fact]
    public void AuthorityUnloadSnapshotSurvivesAWorldRestartWithoutPresentationServices()
    {
        var directory = Directory.CreateTempSubdirectory("scnet-core-save-");
        var rootName = $"core-save-{Guid.NewGuid():N}";
        Storage.RegisterFileSystemRoot(rootName, directory.FullName);
        var worldPath = $"{rootName}:world";
        try
        {
            var value = Terrain.MakeBlockValue(3, 0, 17);
            var shaft = Terrain.ReplaceHumidity(Terrain.ReplaceTemperature(0, 9), 6);
            using (var terrain = new Terrain())
            using (var serializer = new TerrainSerializer24(worldPath))
            using (var saves = new TerrainSaveCoordinator(serializer.SaveSnapshot))
            {
                var chunk = terrain.AllocateChunk(-2, 3);
                chunk.IsLoaded = true;
                chunk.SetShaftValueFast(1, 2, shaft);
                var authority = new TerrainCellAuthority(terrain);
                Assert.True(authority.ChangeCell(-31, 77, 50, value));
                Assert.True(saves.TryQueueChunkForUnload(chunk));
                terrain.FreeChunk(chunk);
                saves.Flush();
            }

            Assert.NotEmpty(Directory.EnumerateFiles(directory.FullName, "*", SearchOption.AllDirectories));
            using var restoredTerrain = new Terrain();
            using var reader = new TerrainSerializer24(worldPath);
            var restored = restoredTerrain.AllocateChunk(-2, 3);
            Assert.True(reader.LoadChunk(restored));
            Assert.Equal(value, restored.GetCellValueFast(1, 77, 2));
            Assert.Equal(shaft, restored.GetShaftValueFast(1, 2));
            Assert.Equal(0, restored.ModificationCounter);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
