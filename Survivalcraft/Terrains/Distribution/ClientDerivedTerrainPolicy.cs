namespace Game.Terrains.Distribution;

/// <summary>
///     Dependency rules used while the legacy derived-data worker is being replaced.
/// </summary>
public static class ClientDerivedTerrainPolicy
{
    public const int LightingDependencyRadius = 2;

    public static bool CanAdvanceLightingDependency(TerrainContentRole role, TerrainChunk neighbor) =>
        role != TerrainContentRole.Replica || neighbor.IsLoaded;

    public static TerrainChunk? FindPendingLightingDependency(
        Terrain terrain,
        TerrainContentRole role,
        TerrainChunk target)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(target);
        if (target.WorkerState != TerrainChunkState.InvalidPropagatedLight)
        {
            return null;
        }

        for (var x = -LightingDependencyRadius; x <= LightingDependencyRadius; x++)
        for (var z = -LightingDependencyRadius; z <= LightingDependencyRadius; z++)
        {
            var neighbor = terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z);
            if (neighbor is { WorkerState: < TerrainChunkState.InvalidPropagatedLight } &&
                CanAdvanceLightingDependency(role, neighbor))
            {
                return neighbor;
            }
        }

        return null;
    }
}
