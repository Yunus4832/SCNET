using Game.Network.Chunks;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;

namespace Game.Terrains;

/// <summary>One world's chunk residency, retained replica leases and save-safe release transactions.</summary>
public sealed class TerrainResidencyManager(
    SubsystemTerrain world,
    TerrainChunkBehaviorNotifier behaviors
)
{
    private readonly Terrain _terrain = world.Terrain;

    private readonly HashSet<Point2> _inactiveClientChunks = [];

    private readonly Dictionary<Point2, long> _remoteChunkLastAccess = [];

    private long _remoteChunkAccessClock;

    private bool _updating;

    private bool _retryRequested;

    public bool IsUpdating => _updating;

    public long DeferredUpdates { get; private set; }

    public readonly record struct UpdateResult(bool Changed, bool RetryRequired);

    public event Action<Point2>? ChunkAllocated;

    public event Action<TerrainChunk>? ChunkReleasing;

    public event Action<TerrainChunk, ulong>? ChunkAllocationRenewed;

    /// <summary>Uses the caller's single lease, yielding it around discard business notifications.</summary>
    public UpdateResult Update(
        TerrainUpdateLocation[] locations,
        TerrainMainWorkRequest locationLease,
        Action<TerrainChunk>? onDiscarding = null
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(locationLease);
        if (!ReferenceEquals(locationLease.executor, world.WorkExecutor))
        {
            throw new ArgumentException("Residency requires its world's foreground request.", nameof(locationLease));
        }

        if (!IsActive()) { return default; }

        if (_updating)
        {
            _retryRequested = true;
            return new UpdateResult(false, true);
        }

        if (!locationLease.HasAccess || !world.Access.IsHeldOnceByCurrentThread)
        {
            throw new SynchronizationLockException("Residency requires the caller's single terrain access lease.");
        }

        _updating = true;
        _retryRequested = false;
        try
        {
            var result = UpdateCore(locations, locationLease, onDiscarding);
            var retry = IsActive() && (result.RetryRequired || _retryRequested);
            if (retry) { DeferredUpdates++; }

            return result with { RetryRequired = retry };
        }
        finally { _updating = false; }
    }

    private bool IsActive() => WorldRuntimeContext.IsProjectActive(world.Project);

    private bool NotifyAllocated(Point2 coords)
    {
        foreach (var callback in ChunkAllocated?.GetInvocationList() ?? [])
        {
            ((Action<Point2>)callback)(coords);
            if (!IsActive()) { return false; }
        }

        return IsActive();
    }

    private bool NotifyReleasing(TerrainChunk chunk)
    {
        foreach (var callback in ChunkReleasing?.GetInvocationList() ?? [])
        {
            ((Action<TerrainChunk>)callback)(chunk);
            if (!IsActive() || chunk.IsDisposed) { return false; }
        }

        return IsActive() && !chunk.IsDisposed;
    }

    private bool NotifyAllocationRenewed(TerrainChunk chunk, ulong oldGeneration)
    {
        foreach (var callback in ChunkAllocationRenewed?.GetInvocationList() ?? [])
        {
            ((Action<TerrainChunk, ulong>)callback)(chunk, oldGeneration);
            if (!IsActive() || chunk.IsDisposed) { return false; }
        }

        return IsActive() && !chunk.IsDisposed;
    }

    internal static ulong RefreshRetainedChunk(
        Terrain terrain,
        TerrainChunk chunk,
        IChunkContentTransport transport,
        ClientTerrainDeltaCoordinator deltas
    )
    {
        transport.Discard(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration));
        if (chunk.IsDisposed || transport is ClientChunkContentTransport client && client.Runtime.IsClosed)
        {
            return chunk.AllocationGeneration;
        }

        deltas.Discard(chunk.Coords);
        var previousGeneration = chunk.AllocationGeneration;
        terrain.RenewChunkAllocation(chunk);
        // Cached cells and uploaded geometry remain usable while this new lease gets a baseline.
        chunk.NeedsContentValidation = true;
        chunk.NeedsContentSnapshot = true;
        chunk.IsRequested = false;
        chunk.NetworkRequestTime = 0;
        return previousGeneration;
    }

    /// <summary>
    ///     检查区块中心是否在任一更新位置的范围内
    /// </summary>
    /// <param name="chunkCenter">区块中心坐标</param>
    /// <param name="locations">更新位置数组</param>
    /// <returns>如果在任一位置的 ContentDistance 范围内则返回 true</returns>
    internal static bool IsChunkInRange(
        Vector2 chunkCenter,
        TerrainUpdateLocation[] locations,
        float distanceMargin = 0f
    )
    {
        for (var i = 0; i < locations.Length; i++)
        {
            if (NetworkTerrainPolicy.IsChunkCenterRelevant(
                    chunkCenter,
                    locations[i].Center,
                    locations[i].ContentDistance + distanceMargin
                ))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     根据更新位置分配和释放区块
    /// </summary>
    /// <param name="locations">更新位置数组</param>
    /// <returns>区块是否发生变化，以及是否需要在后续帧重试</returns>
    internal static bool ShouldDiscardChunk(
        TerrainChunk chunk,
        TerrainUpdateLocation[] locations,
        float retentionMargin,
        IReadOnlySet<Point2> retentionEvictions
    )
    {
        return !IsChunkInRange(chunk.Center, locations, retentionMargin) || retentionEvictions.Contains(chunk.Coords);
    }

    private UpdateResult UpdateCore(
        TerrainUpdateLocation[] locations,
        TerrainMainWorkRequest locationLease,
        Action<TerrainChunk>? onDiscarding
    )
    {
        var result = false;
        var hasDeferredChunkDiscards = false;
        var allocatedChunks = _terrain.AllocatedChunks;
        HashSet<Point2> retentionEvictions = [];
        if (world.UsesClientChunkContent)
        {
            var access = ++_remoteChunkAccessClock;
            foreach (var chunk in allocatedChunks)
            {
                if (!IsChunkInRange(chunk.Center, locations))
                {
                    _inactiveClientChunks.Add(chunk.Coords);
                    continue;
                }

                if (_inactiveClientChunks.Remove(chunk.Coords))
                {
                    var previousGeneration = RefreshRetainedChunk(
                        _terrain,
                        chunk,
                        world.ChunkContentTransport!,
                        world.ClientTerrainDeltas!
                    );
                    if (!IsActive() || chunk.IsDisposed || !NotifyAllocationRenewed(chunk, previousGeneration))
                    {
                        return new UpdateResult(result, false);
                    }
                }

                _remoteChunkLastAccess[chunk.Coords] = access;
            }

            var retained = allocatedChunks
                .Where(chunk => !IsChunkInRange(chunk.Center, locations))
                .Where(chunk => IsChunkInRange(
                        chunk.Center,
                        locations,
                        NetworkTerrainPolicy.ClientChunkRetentionMargin
                    )
                )
                .Select(chunk => (chunk.Coords, _remoteChunkLastAccess.GetValueOrDefault(chunk.Coords)))
                .ToArray();
            retentionEvictions = SelectRetainedChunkCoordsToEvict(
                    retained,
                    NetworkTerrainPolicy.ClientRetainedChunkCapacity
                )
                .ToHashSet();
        }

        foreach (var terrainChunk in allocatedChunks)
        {
            var retentionMargin = world.UsesClientChunkContent
                ? NetworkTerrainPolicy.ClientChunkRetentionMargin
                : 0f;
            if (!ShouldDiscardChunk(terrainChunk, locations, retentionMargin, retentionEvictions))
            {
                continue;
            }

            var saveCoordinator = world.ContentRole == TerrainContentRole.Authority
                ? world.TerrainSaveCoordinator
                : null;
            using var unload = saveCoordinator?.TryReserveUnload(terrainChunk);
            if (saveCoordinator != null && unload == null)
            {
                hasDeferredChunkDiscards = true;
                continue;
            }

            var generation = terrainChunk.AllocationGeneration;
            var notified = false;
            locationLease.YieldAccess();
            try { notified = behaviors.NotifyDiscarding(terrainChunk, onDiscarding); }
            finally { locationLease.TryEnter(Timeout.Infinite); }

            if (!IsActive()) { return new UpdateResult(result, false); }

            if (!locationLease.HasAccess)
            {
                var failure = new InvalidOperationException("Discard callbacks cancelled the terrain access lease.");
                try { world.Project.Dispose(); }
                catch (Exception closingFailure) { throw new AggregateException(failure, closingFailure); }

                throw failure;
            }

            if (terrainChunk.IsDisposed ||
                !ReferenceEquals(
                    _terrain.GetChunkAtCoords(terrainChunk.Coords.X, terrainChunk.Coords.Y),
                    terrainChunk
                ) ||
                terrainChunk.AllocationGeneration != generation)
            {
                var failure = new InvalidOperationException("Discard callbacks changed the chunk's allocation lease.");
                try { world.Project.Dispose(); }
                catch (Exception closingFailure) { throw new AggregateException(failure, closingFailure); }

                throw failure;
            }

            if (!notified) { return new UpdateResult(result, false); }

            world.ChunkDistribution?.OnChunkRemoved(terrainChunk);
            if (!IsActive() || terrainChunk.IsDisposed) { return new UpdateResult(result, false); }

            if (saveCoordinator != null)
            {
                if (!unload!.Commit(terrainChunk))
                {
                    throw new InvalidOperationException(
                        "A reserved terrain unload could not commit its final contents."
                    );
                }
            }
            else if (world.ContentRole == TerrainContentRole.Authority)
            {
                (world.TerrainSerializer ??
                 throw new InvalidOperationException("Authority terrain requires a disk serializer."))
                    .SaveChunk(terrainChunk);
            }

            result = true;
            world.ClientTerrainDeltas?.Discard(terrainChunk.Coords);
            world.ChunkContentTransport?.Discard(
                new ChunkAllocationId(terrainChunk.Coords, terrainChunk.AllocationGeneration)
            );
            if (!IsActive() || terrainChunk.IsDisposed) { return new UpdateResult(result, false); }

            _inactiveClientChunks.Remove(terrainChunk.Coords);
            if (!NotifyReleasing(terrainChunk)) { return new UpdateResult(result, false); }

            _terrain.FreeChunk(terrainChunk);
            _remoteChunkLastAccess.Remove(terrainChunk.Coords);
        }

        if (hasDeferredChunkDiscards)
        {
            return new UpdateResult(result, true);
        }

        for (var j = 0; j < locations.Length; j++)
        {
            var point = Terrain.ToChunk(locations[j].Center - new Vector2(locations[j].ContentDistance));
            var point2 = Terrain.ToChunk(locations[j].Center + new Vector2(locations[j].ContentDistance));
            for (var k = point.X; k <= point2.X; k++)
            {
                for (var l = point.Y; l <= point2.Y; l++)
                {
                    var chunkCenter = new Vector2((k + 0.5f) * 16f, (l + 0.5f) * 16f);
                    var chunkAtCoords = _terrain.GetChunkAtCoords(k, l);
                    if (chunkAtCoords == null)
                    {
                        if (!IsChunkInRange(chunkCenter, locations))
                        {
                            continue;
                        }

                        result = true;
                        _terrain.AllocateChunk(k, l);
                        if (world.UsesClientChunkContent)
                        {
                            _remoteChunkLastAccess[new Point2(k, l)] = _remoteChunkAccessClock;
                        }

                        if (!NotifyAllocated(new Point2(k, l))) { return new UpdateResult(result, false); }
                    }
                    else if (chunkAtCoords.Coords.X != k || chunkAtCoords.Coords.Y != l)
                    {
                        Log.Error("Chunk wraparound detected at {0}", chunkAtCoords.Coords);
                    }
                }
            }
        }

        return new UpdateResult(result, false);
    }

    internal static IReadOnlyList<Point2> SelectRetainedChunkCoordsToEvict(
        IEnumerable<(Point2 Coords, long LastAccess)> retainedChunks,
        int capacity
    )
    {
        ArgumentNullException.ThrowIfNull(retainedChunks);
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        return retainedChunks
            .OrderByDescending(item => item.LastAccess)
            .ThenBy(item => item.Coords.X)
            .ThenBy(item => item.Coords.Y)
            .Skip(capacity)
            .Select(item => item.Coords)
            .ToArray();
    }
}
