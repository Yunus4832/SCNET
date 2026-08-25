using Game.Network;

namespace Game.Terrains.Distribution;

/// <summary>
/// Incremental in-process authority used by Local mode. It owns authoritative allocations while
/// exposing the same request/snapshot transport contract as a remote server.
/// </summary>
public sealed class LocalChunkContentHost : IChunkContentTransport
{
    private readonly Terrain _authorityTerrain;
    private readonly TerrainChunkContentAuthority _authority;
    private readonly AuthoritativeChunkGenerationPipeline _generation;
    private readonly InProcessChunkContentTransport _transport;
    private readonly PendingChunkRequestQueue _pending = new();
    private readonly Func<TerrainChunk, bool> _prepareRelease;
    private readonly Lock _lock = new();

    public LocalChunkContentHost(
        Terrain authorityTerrain,
        AuthoritativeChunkGenerationPipeline generation,
        Func<TerrainChunk, bool>? prepareRelease = null)
    {
        _authorityTerrain = authorityTerrain ?? throw new ArgumentNullException(nameof(authorityTerrain));
        _generation = generation ?? throw new ArgumentNullException(nameof(generation));
        _prepareRelease = prepareRelease ?? (_ => true);
        _authority = new TerrainChunkContentAuthority(authorityTerrain);
        CellAuthority = new TerrainCellAuthority(authorityTerrain);
        _transport = new InProcessChunkContentTransport(_authority);
    }

    public Terrain AuthorityTerrain => _authorityTerrain;

    public TerrainCellAuthority CellAuthority { get; }

    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count;
            }
        }
    }

    public void Request(IReadOnlyList<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        lock (_lock)
        {
            _pending.EnqueueRange(requests);
        }
    }

    /// <summary>
    /// Advances at most <paramref name="maximumSteps"/> allocation/load/generation steps.
    /// Incomplete requests rotate to the tail to preserve fairness.
    /// </summary>
    public int Update(int maximumSteps)
    {
        if (maximumSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSteps));
        }

        lock (_lock)
        {
            var requests = _pending.Take(maximumSteps).ToArray();
            var steps = 0;
            foreach (var request in requests)
            {
                var coords = request.Allocation.Coords;
                var chunk = _authorityTerrain.GetChunkAtCoords(coords.X, coords.Y) ??
                            _authorityTerrain.AllocateChunk(coords.X, coords.Y);
                if (chunk.WorkerState < TerrainChunkState.InvalidLight)
                {
                    _generation.TryAdvance(chunk);
                    steps++;
                }

                _pending.Remove(coords);
                if (chunk is { IsLoaded: true, WorkerState: >= TerrainChunkState.InvalidLight })
                {
                    _transport.Request([request]);
                }
                else
                {
                    _pending.Enqueue(request);
                }
            }

            return steps;
        }
    }

    public bool Release(Point2 coords)
    {
        lock (_lock)
        {
            var chunk = _authorityTerrain.GetChunkAtCoords(coords.X, coords.Y);
            if (chunk == null)
            {
                _pending.Remove(coords);
                _transport.Discard(coords);
                return false;
            }

            if (!_prepareRelease(chunk))
            {
                return false;
            }

            _pending.Remove(coords);
            _transport.Discard(coords);
            _authorityTerrain.FreeChunk(chunk);
            chunk.Dispose();
            return true;
        }
    }

    public int DrainReceived(ICollection<ClientChunkSnapshot> destination) =>
        _transport.DrainReceived(destination);

    public int DrainFailed(ICollection<ChunkAllocationId> destination) =>
        _transport.DrainFailed(destination);

    public int DrainDeltas(ICollection<TerrainCellDelta> destination) =>
        _transport.DrainDeltas(destination);
}
