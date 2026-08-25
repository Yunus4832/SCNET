using Game.Network;

namespace Game.Terrains.Distribution;

/// <summary>
/// Authoritative snapshot source backed by generated or loaded server terrain.
/// </summary>
public sealed class TerrainChunkContentAuthority(Terrain terrain) : IChunkContentAuthority
{
    private readonly Terrain _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));

    public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot)
    {
        var chunk = _terrain.GetChunkAtCoords(coords.X, coords.Y);
        if (chunk is not { IsLoaded: true } ||
            chunk.WorkerState < TerrainChunkState.InvalidLight)
        {
            snapshot = null!;
            return false;
        }

        using var captured = NetworkChunkSnapshot.TryCapture(chunk);
        if (captured == null)
        {
            snapshot = null!;
            return false;
        }

        snapshot = new AuthorityChunkSnapshot(
            coords,
            captured.Revision + 1,
            captured.Cells.AsSpan(0, AuthorityChunkSnapshot.CellCount).ToArray(),
            captured.Shafts.AsSpan(0, AuthorityChunkSnapshot.ShaftCount).ToArray());
        return true;
    }

    public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
    {
        var chunk = _terrain.GetChunkAtCoords(coords.X, coords.Y);
        if (chunk is not { IsLoaded: true } ||
            chunk.WorkerState < TerrainChunkState.InvalidLight)
        {
            descriptor = default;
            return false;
        }

        descriptor = new AuthorityChunkDescriptor(coords, chunk.NetworkContentRevision + 1);
        return true;
    }
}
