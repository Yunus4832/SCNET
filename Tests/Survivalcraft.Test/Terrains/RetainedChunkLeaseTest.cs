using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class RetainedChunkLeaseTest
{
    [Fact]
    public void PresentationRenewalKeepsCachedGeometryDrawableForTheNewLease()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 10;
        var presentation = presentations.Get(chunk);
        presentation.GeometryUploaded = true;
        var geometry = presentation.ChunkSliceGeometries;
        var previousGeneration = chunk.AllocationGeneration;
        terrain.RenewChunkAllocation(chunk);
        chunk.NeedsContentValidation = true;
        chunk.NeedsContentSnapshot = true;
        presentations.RenewAllocation(chunk, previousGeneration);
        Assert.Same(geometry, presentations.Get(chunk).ChunkSliceGeometries);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
    }
}
