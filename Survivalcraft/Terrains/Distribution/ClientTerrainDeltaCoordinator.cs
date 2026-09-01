namespace Game.Terrains.Distribution;

/// <summary>
///     Orders authoritative cell deltas against installed chunk snapshot versions.
///     A version gap invalidates the replica so the normal snapshot request path restores convergence.
/// </summary>
public sealed class ClientTerrainDeltaCoordinator(
    Terrain terrain,
    Action<TerrainCellDelta> apply)
{
    public const int MaximumPendingDeltasPerChunk = 256;

    private readonly Terrain _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
    private readonly Action<TerrainCellDelta> _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    private readonly Dictionary<Point2, SortedDictionary<long, TerrainCellDelta>> _pending = [];
    private readonly Dictionary<Point2, long> _requiredVersions = [];

    public TerrainDeltaApplyResult Receive(TerrainCellDelta delta)
    {
        Validate(delta);
        var coords = Terrain.ToChunk(delta.Cell.X, delta.Cell.Z);
        var chunk = _terrain.GetChunkAtCoords(coords.X, coords.Y);
        if (chunk == null || delta.ResultContentVersion <= chunk.NetworkContentVersion)
        {
            return TerrainDeltaApplyResult.Ignored;
        }

        if (!_pending.TryGetValue(coords, out var deltas))
        {
            deltas = [];
            _pending.Add(coords, deltas);
        }

        deltas[delta.BaseContentVersion] = delta;
        _requiredVersions[coords] = Math.Max(
            _requiredVersions.GetValueOrDefault(coords),
            delta.ResultContentVersion);

        if (deltas.Count <= MaximumPendingDeltasPerChunk)
        {
            return chunk.IsLoaded
                ? ApplyAvailable(chunk)
                : TerrainDeltaApplyResult.Buffered;
        }

        deltas.Clear();
        InvalidateForSnapshot(chunk);
        return TerrainDeltaApplyResult.ResyncRequired;
    }

    public TerrainDeltaApplyResult OnContentInstalled(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return ApplyAvailable(chunk);
    }

    public void Discard(Point2 coords)
    {
        _pending.Remove(coords);
        _requiredVersions.Remove(coords);
    }

    private TerrainDeltaApplyResult ApplyAvailable(TerrainChunk chunk)
    {
        var coords = chunk.Coords;
        if (!_pending.TryGetValue(coords, out var deltas))
        {
            return TerrainDeltaApplyResult.Ignored;
        }

        foreach (var stale in deltas
                     .Where(item => item.Value.ResultContentVersion <= chunk.NetworkContentVersion)
                     .Select(item => item.Key)
                     .ToArray())
        {
            deltas.Remove(stale);
        }

        var applied = false;
        while (deltas.Remove(chunk.NetworkContentVersion, out var delta))
        {
            _apply(delta);
            chunk.NetworkContentVersion = delta.ResultContentVersion;
            applied = true;
        }

        var requiredVersion = _requiredVersions.GetValueOrDefault(coords);
        if (chunk.NetworkContentVersion < requiredVersion)
        {
            Log.Warning(
                $"Terrain replica version gap at {coords}: installed={chunk.NetworkContentVersion}, " +
                $"required={requiredVersion}, pending={deltas.Count}; requesting full snapshot.");
            InvalidateForSnapshot(chunk);
            return TerrainDeltaApplyResult.ResyncRequired;
        }

        _pending.Remove(coords);
        _requiredVersions.Remove(coords);
        return applied ? TerrainDeltaApplyResult.Applied : TerrainDeltaApplyResult.Ignored;
    }

    private static void InvalidateForSnapshot(TerrainChunk chunk)
    {
        chunk.IsLoaded = false;
        chunk.IsRequested = false;
        chunk.NetworkRequestTime = 0.0;
        chunk.NetworkContentReceiveTime = 0.0;
        chunk.ClientGeometryContentVersion = 0;
        chunk.GeometryUploaded = false;
        chunk.NewGeometryData = false;
        chunk.MainThreadState = TerrainChunkState.NotLoaded;
        chunk.WorkerState = TerrainChunkState.NotLoaded;
        chunk.ResetStateExchange();
    }

    private static void Validate(TerrainCellDelta delta)
    {
        if (delta.Cell.Y is < 0 or >= 256 ||
            delta.BaseContentVersion <= 0 ||
            delta.ResultContentVersion != delta.BaseContentVersion + 1)
        {
            throw new InvalidDataException("Invalid terrain cell delta metadata.");
        }
    }
}

public enum TerrainDeltaApplyResult
{
    Ignored,
    Buffered,
    Applied,
    ResyncRequired
}
