namespace Game.Terrains.Distribution;

/// <summary>
///     CPU lighting dependencies and the client mesh prerequisites.
/// </summary>
public static class ClientDerivedTerrainPolicy
{
    public const int LightingDependencyRadius = 1;

    public static bool CanAdvanceLightingDependency(TerrainContentRole role, TerrainChunk neighbor) =>
        role != TerrainContentRole.Replica || neighbor.IsLoaded;

    public static TerrainChunk? FindPendingLightingDependency(
        Terrain terrain,
        TerrainContentRole role,
        TerrainChunk target
    )
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(target);
        if (target.WorkerState != TerrainChunkState.InvalidPropagatedLight)
        {
            return null;
        }

        for (var x = -LightingDependencyRadius; x <= LightingDependencyRadius; x++)
        {
            for (var z = -LightingDependencyRadius; z <= LightingDependencyRadius; z++)
            {
                var neighbor = terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z);
                if (neighbor is { WorkerState: < TerrainChunkState.InvalidPropagatedLight } &&
                    CanAdvanceLightingDependency(role, neighbor))
                {
                    return neighbor;
                }
            }
        }

        return null;
    }

    public static TerrainChunk? FindPendingGeometryDependency(
        Terrain terrain,
        TerrainContentRole role,
        TerrainChunk target
    )
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(target);
        if (target.WorkerState != TerrainChunkState.Ready)
        {
            return null;
        }

        for (var x = -LightingDependencyRadius; x <= LightingDependencyRadius; x++)
        {
            for (var z = -LightingDependencyRadius; z <= LightingDependencyRadius; z++)
            {
                var neighbor = terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z);
                if (neighbor is { WorkerState: < TerrainChunkState.Ready } &&
                    CanAdvanceLightingDependency(role, neighbor))
                {
                    return neighbor;
                }
            }
        }

        return null;
    }
}
