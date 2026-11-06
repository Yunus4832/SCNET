using Game.Terrains.Rendering;

namespace Game.Terrains.Distribution;

/// <summary>
///     Owns the client-only lifecycle from installed authoritative contents to derived geometry.
/// </summary>
public sealed class ClientChunkDerivationPipeline(TerrainChunkPresentationStore presentations)
{
    public void Begin(TerrainChunk target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var presentation = presentations.Get(target);

        presentation.RequestMeshRebuild();
        presentation.NewGeometryData = false;
    }

    public static bool CompleteGeometry(TerrainChunk target, TerrainChunkPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(target);
        return presentation.CompleteMeshBuild(target);
    }

    public static bool HasCurrentGeometry(
        TerrainContentRole role,
        TerrainChunk target,
        TerrainChunkPresentation presentation
    )
    {
        ArgumentNullException.ThrowIfNull(target);
        return role != TerrainContentRole.Replica ||
               target is { IsLoaded: true } &&
               target.NetworkContentVersion > 0 &&
               presentation.HasCurrentGeometry(target);
    }

    public static bool CanDraw(TerrainContentRole role, TerrainChunk target, TerrainChunkPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(target);
        return role == TerrainContentRole.Replica
            // Keep the last uploaded geometry visible while a newer content version is
            // being lit and rebuilt, including baseline refreshes within this allocation.
            // A new allocation starts without uploaded geometry.
            ? target.IsLoaded && presentation.GeometryUploaded
            : presentation.GeometryUploaded;
    }
}
