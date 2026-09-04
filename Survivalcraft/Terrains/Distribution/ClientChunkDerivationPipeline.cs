namespace Game.Terrains.Distribution;

/// <summary>
///     Owns the client-only lifecycle from installed authoritative contents to derived geometry.
/// </summary>
public sealed class ClientChunkDerivationPipeline(Terrain terrain)
{
    private readonly Terrain _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));

    public void Begin(TerrainChunk target)
    {
        ArgumentNullException.ThrowIfNull(target);
        InvalidateLoadedNeighbors(target);

        // Pending transitions from before content arrival belong to an obsolete lifecycle.
        target.MainThreadState = TerrainChunkState.InvalidLight;
        target.WorkerState = TerrainChunkState.InvalidLight;
        target.ResetStateExchange();
        target.ClientGeometryContentVersion = 0;
        target.NewGeometryData = false;
    }

    public static void CompleteGeometry(TerrainChunk target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.ClientGeometryContentVersion = target.NetworkContentVersion;
    }

    public static bool HasCurrentGeometry(TerrainContentRole role, TerrainChunk target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return role != TerrainContentRole.Replica ||
               target is { IsLoaded: true } &&
               target.NetworkContentVersion > 0 &&
               target.ClientGeometryContentVersion == target.NetworkContentVersion;
    }

    public static bool CanDraw(TerrainContentRole role, TerrainChunk target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return role == TerrainContentRole.Replica
            // Keep the last uploaded geometry visible while a newer content version is
            // being lit and rebuilt. Initial content and explicit resyncs clear the
            // upload flag, so they still cannot expose absent or unrelated geometry.
            ? target is { IsLoaded: true, GeometryUploaded: true }
            : target.GeometryUploaded;
    }

    private void InvalidateLoadedNeighbors(TerrainChunk target)
    {
        const int radius = ClientDerivedTerrainPolicy.LightingDependencyRadius;
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                var neighbor = _terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + y);
                if (neighbor is not { IsLoaded: true })
                {
                    continue;
                }

                if (neighbor.MainThreadState > TerrainChunkState.InvalidLight)
                {
                    neighbor.MainThreadState = TerrainChunkState.InvalidLight;
                    neighbor.InvalidateSliceContentsHashes();
                }

                TerrainChunkStateExchange.RequestDowngrade(neighbor, TerrainChunkState.InvalidLight);
            }
        }
    }
}
