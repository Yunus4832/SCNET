using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainAllocationGenerationTest
{
    [Fact]
    public void RealUnloadInvalidatesOldCompletionsAndCannotReopenTheRetiredContents()
    {
        using var terrain = new Terrain();
        var old = terrain.AllocateChunk(2, 3);
        old.HasContents = true;
        var request = old.DerivedRevisions.Capture();
        Assert.True(old.DerivedRevisions.PublishHeights(request));
        terrain.FreeChunk(old);
        Assert.True(old.IsDisposed);
        Assert.False(old.HasContents);
        Assert.False(old.DerivedRevisions.PublishHeights(request));
        Assert.False(old.DerivedRevisions.PublishLight(request));
        Assert.Throws<ObjectDisposedException>(() => old.HasContents = true);
        var current = terrain.AllocateChunk(2, 3);
        Assert.False(current.HasContents);
        Assert.False(current.HasCurrentLight);
        Assert.True(current.AllocationGeneration > old.AllocationGeneration);
    }

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
