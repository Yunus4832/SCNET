using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

internal static class TerrainMeshTestData
{
    public static void Complete(TerrainChunk chunk, TerrainChunkPresentation presentation)
    {
        presentation.PrepareMeshUpdate(chunk);
        presentation.CompleteFirstPass();
        Assert.True(presentation.CompleteMeshBuild(chunk));
    }

    public static void CompleteAtVersion(TerrainChunk chunk, TerrainChunkPresentation presentation, long version)
    {
        var installed = chunk.NetworkContentVersion;
        chunk.NetworkContentVersion = version;
        Complete(chunk, presentation);
        chunk.NetworkContentVersion = installed;
    }
}
