using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainSliceGeometryCacheTest
{
    [Fact]
    public void ReturningToUploadedContentRebuildsTheInterruptedCpuGeometry()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var presentation = new TerrainChunkPresentation();
        var geometry = new TerrainGeometry();
        presentation.ChunkSliceGeometries[0] = geometry;
        const int contentA = 100;
        const int contentB = 200;
        chunk.NetworkContentVersion = 1;
        presentation.PrepareMeshUpdate(chunk);
        GenerateSlice(presentation, geometry, contentA);
        presentation.CompleteFirstPass();
        Assert.True(presentation.CompleteMeshBuild(chunk));
        presentation.GeometryUploaded = true;

        chunk.NetworkContentVersion = 2;
        presentation.PrepareMeshUpdate(chunk);
        GenerateSlice(presentation, geometry, contentB);
        presentation.CompleteFirstPass();
        Assert.Equal(contentB, geometry.SubsetOpaque.Indices[0]);

        chunk.NetworkContentVersion = 3;
        Assert.False(presentation.CompleteMeshBuild(chunk));
        presentation.PrepareMeshUpdate(chunk);
        Assert.True(GenerateSlice(presentation, geometry, contentA));
        presentation.CompleteFirstPass();
        Assert.True(presentation.CompleteMeshBuild(chunk));
        Assert.Equal(contentA, geometry.SubsetOpaque.Indices[0]);
        Assert.True(presentation.GeometryUploaded);
        Assert.False(GenerateSlice(presentation, geometry, contentA));
    }

    [Fact]
    public void InterruptedSliceIsNeverReusedEvenWhenTheRequestedHashReturns()
    {
        using var presentation = new TerrainChunkPresentation();
        var geometry = new TerrainGeometry();
        GenerateSlice(presentation, geometry, 10);
        Assert.True(presentation.BeginSliceGeometryUpdate(0, 20));
        geometry.SubsetOpaque.Indices.Clear();
        // A generator exception leaves no completed CPU geometry for either hash.
        Assert.True(presentation.BeginSliceGeometryUpdate(0, 10));
        Assert.True(presentation.BeginSliceGeometryUpdate(0, 20));
        presentation.CompleteSliceGeometryUpdate(0, 20);
        Assert.False(presentation.BeginSliceGeometryUpdate(0, 20));
        presentation.InvalidateSliceContentsHashes();
        Assert.True(presentation.BeginSliceGeometryUpdate(0, 20));
    }

    private static bool GenerateSlice(TerrainChunkPresentation presentation, TerrainGeometry geometry, int content)
    {
        if (!presentation.BeginSliceGeometryUpdate(0, content)) { return false; }

        geometry.SubsetOpaque.Indices.Clear();
        geometry.SubsetOpaque.Indices.Add(content);
        presentation.CompleteSliceGeometryUpdate(0, content);
        return true;
    }
}
