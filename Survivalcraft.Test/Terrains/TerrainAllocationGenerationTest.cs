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

    [Fact]
    public void ContentRevisionAdvancesAcrossEmptyAllocationsWithoutAffectingOtherCoordinates()
    {
        using var terrain = new Terrain();
        var previous = terrain.AllocateChunk(2, 3);
        previous.SetCellValueFast(0, 64, 0, 7);
        var revision = previous.NetworkContentRevision;
        terrain.FreeChunk(previous);

        for (var i = 0; i < 3; i++)
        {
            var current = terrain.AllocateChunk(2, 3);
            Assert.True(current.NetworkContentRevision > revision);
            revision = current.NetworkContentRevision;
            terrain.FreeChunk(current);
        }

        Assert.Equal(0, terrain.AllocateChunk(3, 3).NetworkContentRevision);
        using var anotherWorld = new Terrain();
        Assert.Equal(0, anotherWorld.AllocateChunk(2, 3).NetworkContentRevision);
    }
}
