using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Distribution;

/// <summary>
///     Single installation boundary for authoritative contents entering a client terrain.
/// </summary>
public sealed class ClientChunkContentCoordinator(Terrain terrain)
{
    private readonly Terrain _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));

    public event Action<TerrainChunk>? ContentInstalled;

    public bool TryInstall(ClientChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var target = _terrain.GetChunkAtCoords(
            snapshot.Allocation.Coords.X,
            snapshot.Allocation.Coords.Y);
        if (!CanInstall(target, snapshot.Allocation, snapshot.ContentVersion))
        {
            return false;
        }

        target.Cells = snapshot.Cells.ToArray();
        target.Shafts = snapshot.Shafts.ToArray();
        target.NetworkContentVersion = snapshot.ContentVersion;
        target.IsLoaded = true;
        ContentInstalled?.Invoke(target);
        return true;
    }

    internal static bool CanInstall(
        [NotNullWhen(true)] TerrainChunk? target,
        ChunkAllocationId allocation,
        long contentVersion) =>
        target is { IsLoaded: false } &&
        target.Coords == allocation.Coords &&
        target.AllocationGeneration == allocation.Generation &&
        contentVersion > target.NetworkContentVersion;
}
