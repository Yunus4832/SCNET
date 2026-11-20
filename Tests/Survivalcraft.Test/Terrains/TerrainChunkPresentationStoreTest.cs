using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainChunkPresentationStoreTest
{
    [Fact]
    public void WorldsOwnIndependentPresentationAndDisposal()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var first = new TerrainChunkPresentationStore();
        using var second = new TerrainChunkPresentationStore();
        var firstPresentation = first.Get(chunk);
        var secondPresentation = second.Get(chunk);
        firstPresentation.GeometryUploaded = true;
        first.Dispose();

        Assert.NotSame(firstPresentation, secondPresentation);
        Assert.False(firstPresentation.GeometryUploaded);
        Assert.Same(secondPresentation, second.Get(chunk));
        Assert.Throws<ObjectDisposedException>(() => first.Get(chunk));
    }

    [Fact]
    public void RetainedAllocationRequiresExplicitRenewalAndRejectsOldWork()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var generation = chunk.AllocationGeneration;
        var presentation = presentations.Get(chunk, generation);
        presentation.GeometryUploaded = true;
        terrain.RenewChunkAllocation(chunk);

        Assert.Throws<InvalidOperationException>(() => presentations.Get(chunk));
        presentations.RenewAllocation(chunk, generation);

        Assert.Same(presentation, presentations.Get(chunk));
        Assert.True(presentation.GeometryUploaded);
        Assert.Throws<InvalidOperationException>(() => presentations.Get(chunk, generation));
    }

    [Fact]
    public void ReleasedChunkCannotRecreateVisualResourcesAndReallocationIsIndependent()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var previous = presentations.Get(chunk);
        previous.GeometryUploaded = true;
        presentations.Release(chunk);
        terrain.FreeChunk(chunk);
        var replacement = terrain.AllocateChunk(0, 0);

        Assert.False(previous.GeometryUploaded);
        Assert.Throws<InvalidOperationException>(() => presentations.Get(chunk));
        Assert.NotSame(previous, presentations.Get(replacement));
    }
}
