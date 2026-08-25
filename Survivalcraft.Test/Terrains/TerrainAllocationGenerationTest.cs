using Game.Terrains;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainAllocationGenerationTest
{
    [Fact]
    public void ReallocatingCoordinateAdvancesGeneration()
    {
        var terrain = new Terrain();
        var first = terrain.AllocateChunk(2, 3);
        terrain.FreeChunk(first);
        var second = terrain.AllocateChunk(2, 3);

        Assert.Equal(1UL, first.AllocationGeneration);
        Assert.Equal(2UL, second.AllocationGeneration);
    }
}
