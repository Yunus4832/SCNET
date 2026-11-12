using Game.Terrains;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainMeshLightingInputsTest
{
    [Fact]
    public void SliceHashTracksSpatialLightWithoutChangingCellContents()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(8, 64, 8, Terrain.MakeBlockValue(1));
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++) { chunk.SetTopHeightFast(x, z, 64); }
        }

        TerrainMeshTestData.PrepareLighting(chunk);
        var input = terrain.Lighting.CaptureInput(chunk);
        var bright = new TerrainLightSnapshot(new TerrainLightChannels(15, 0));
        Assert.True(terrain.Lighting.TryPublish(chunk, input, bright));
        var builder = new TerrainMeshBuilder(terrain, null!, null!);
        var before = builder.CalculateChunkSliceContentsHash(chunk, 4);
        var cells = (int[])chunk.Cells.Clone();
        var content = chunk.NetworkContentRevision;
        var dark = new TerrainLightSnapshot(new TerrainLightChannels(0, 0));
        Assert.True(terrain.Lighting.TryPublish(chunk, input, dark));
        Assert.NotEqual(before, builder.CalculateChunkSliceContentsHash(chunk, 4));
        Assert.Equal(cells, chunk.Cells);
        Assert.Equal(content, chunk.NetworkContentRevision);
    }

    [Fact]
    public void PropagationRequestWithUnchangedValuesRetainsCompletedGeometry()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var mesh = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, mesh);
        mesh.MarkUploaded();
        chunk.DerivedRevisions.InvalidatePropagation();
        Assert.True(mesh.HasCurrentGeometry(chunk));
        Assert.False(chunk.HasCurrentLight);
        Assert.True(chunk.DerivedRevisions.PublishLight(chunk.DerivedRevisions.Capture(), false));
        Assert.True(chunk.HasCurrentLight);
        Assert.True(mesh.HasCurrentGeometry(chunk));
        Assert.True(mesh.HasCurrentUploadedGeometry(chunk));
    }
}
