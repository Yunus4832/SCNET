namespace Game.Terrains.Distribution;

/// <summary>
///     Orders authoritative cell deltas against installed chunk snapshot versions.
///     A version gap requests a new baseline while retaining the installed replica until replacement.
/// </summary>
public sealed class ClientTerrainDeltaCoordinator(
    Terrain terrain,
    Action<TerrainCellDelta> apply
)
{
    public const int MaximumPendingDeltasPerChunk = 256;

    public const int MaximumPendingDeltas = 8192;

    public int PendingDeltaCount { get; private set; }

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

        if (!deltas.ContainsKey(delta.BaseContentVersion))
        {
            if (PendingDeltaCount == MaximumPendingDeltas || deltas.Count == MaximumPendingDeltasPerChunk)
            {
                return RequireSnapshot(
                    new ChunkContentResync(
                        new ChunkAllocationId(coords, chunk.AllocationGeneration),
                        delta.ResultContentVersion
                    )
                );
            }

            PendingDeltaCount++;
        }

        deltas[delta.BaseContentVersion] = delta;
        _requiredVersions[coords] = Math.Max(
            _requiredVersions.GetValueOrDefault(coords),
            delta.ResultContentVersion
        );

        return chunk.HasContents && !chunk.NeedsContentValidation
            ? ApplyAvailable(chunk)
            : TerrainDeltaApplyResult.Buffered;
    }

    public TerrainDeltaApplyResult RequireSnapshot(ChunkContentResync request)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.RequiredContentVersion);
        var coords = request.Allocation.Coords;
        var chunk = _terrain.GetChunkAtCoords(coords.X, coords.Y);
        if (chunk == null || chunk.AllocationGeneration != request.Allocation.Generation)
        {
            return TerrainDeltaApplyResult.Ignored;
        }

        var required = Math.Max(_requiredVersions.GetValueOrDefault(coords), request.RequiredContentVersion);
        if (chunk.NetworkContentVersion >= required) { return TerrainDeltaApplyResult.Ignored; }

        _requiredVersions[coords] = required;
        if (!_pending.TryGetValue(coords, out var deltas))
        {
            deltas = [];
            _pending.Add(coords, deltas);
        }

        PendingDeltaCount -= deltas.Count;
        deltas.Clear();
        RequestSnapshot(chunk);
        return TerrainDeltaApplyResult.ResyncRequired;
    }

    public TerrainDeltaApplyResult OnContentInstalled(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return ApplyAvailable(chunk);
    }

    public void Discard(Point2 coords)
    {
        if (_pending.Remove(coords, out var deltas)) { PendingDeltaCount -= deltas.Count; }

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
            PendingDeltaCount--;
        }

        var applied = false;
        while (deltas.Remove(chunk.NetworkContentVersion, out var delta))
        {
            PendingDeltaCount--;
            _apply(delta);
            chunk.NetworkContentVersion = delta.ResultContentVersion;
            applied = true;
        }

        var requiredVersion = _requiredVersions.GetValueOrDefault(coords);
        if (chunk.NetworkContentVersion < requiredVersion)
        {
            if (!chunk.NeedsContentSnapshot)
            {
                Log.Warning(
                    $"Terrain replica version gap at {coords}: installed={chunk.NetworkContentVersion}, " +
                    $"required={requiredVersion}, pending={deltas.Count}; requesting full snapshot."
                );
            }

            RequestSnapshot(chunk);
            return TerrainDeltaApplyResult.ResyncRequired;
        }

        _pending.Remove(coords);
        _requiredVersions.Remove(coords);
        chunk.NeedsContentSnapshot = false;
        return applied ? TerrainDeltaApplyResult.Applied : TerrainDeltaApplyResult.Ignored;
    }

    private static void RequestSnapshot(TerrainChunk chunk)
    {
        if (chunk.NeedsContentSnapshot) { return; }

        chunk.NeedsContentSnapshot = true;
        chunk.IsRequested = false;
        chunk.NetworkRequestTime = 0.0;
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
    Ignored = 0,

    Buffered = 1,

    Applied = 2,

    ResyncRequired = 3
}
