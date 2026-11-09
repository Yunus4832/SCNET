using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainChunkStorageTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9)]
    public void RemovingACollisionEntryPreservesTheWrappedProbeChainAndAllowsReallocation(int removed)
    {
        using var terrain = new Terrain();
        var chunks = Enumerable.Range(0, 10).Select(z => terrain.AllocateChunk(65535, z * 256)).ToArray();
        terrain.FreeChunk(chunks[removed]);
        Assert.True(chunks[removed].IsDisposed);
        Assert.Null(terrain.GetChunkAtCoords(65535, removed * 256));
        for (var z = 0; z < chunks.Length; z++)
        {
            if (z != removed) { Assert.Same(chunks[z], terrain.GetChunkAtCoords(65535, z * 256)); }
        }

        var replacement = terrain.AllocateChunk(65535, removed * 256);
        Assert.Equal(2UL, replacement.AllocationGeneration);
        Assert.Same(replacement, terrain.GetChunkAtCoords(65535, removed * 256));
        Assert.False(replacement.IsDisposed);
    }
}
