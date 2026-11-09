using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

internal static class TerrainMeshTestData
{
    public static void Complete(TerrainChunk chunk, TerrainChunkPresentation presentation)
    {
        PrepareLighting(chunk);
        foreach (var neighbor in chunk.Terrain.AllocatedChunks)
        {
            if (neighbor.HasContents) { PrepareLighting(neighbor); }
        }

        presentation.PrepareMeshUpdate(chunk);
        presentation.CompleteFirstPass();
        Assert.True(presentation.CompleteMeshBuild(chunk));
    }

    public static void PrepareLighting(TerrainChunk chunk)
    {
        chunk.HasContents = true;
        var request = chunk.DerivedRevisions.Capture();
        if (!chunk.DerivedRevisions.HasCurrentHeights) { Assert.True(chunk.DerivedRevisions.PublishHeights(request)); }

        chunk.DerivedRevisions.PublishSunlight(request);
        if (!chunk.DerivedRevisions.HasCurrentLight) { Assert.True(chunk.DerivedRevisions.PublishLight(request)); }
    }

    public static void CompleteAtVersion(TerrainChunk chunk, TerrainChunkPresentation presentation, long version)
    {
        var installed = chunk.NetworkContentVersion;
        chunk.NetworkContentVersion = version;
        Complete(chunk, presentation);
        chunk.NetworkContentVersion = installed;
    }
}
