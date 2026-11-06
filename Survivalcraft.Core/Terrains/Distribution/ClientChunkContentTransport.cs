using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Game.Terrains.Distribution;

/// <summary>Owns requested allocation lifetimes and bounded complete-snapshot staging for either client connection.</summary>
public sealed class ClientChunkContentTransport : IChunkContentTransport, IDisposable
{
    public const long MaximumQueuedSnapshotBytes = 16L * 1024 * 1024;

    public const int MaximumQueuedDeltas = 8192;

    public const int MaximumQueuedDeltasPerChunk = 256;

    public const int MaximumDeltasPerDrain = 1024;

    public const long SnapshotBytes = 4L * AuthorityChunkSnapshot.CellCount + 8L * AuthorityChunkSnapshot.ShaftCount;

    private readonly Lock _gate = new();

    private readonly WorldRuntimeContext _runtime;

    private static readonly ConditionalWeakTable<WorldRuntimeContext, ClientChunkContentTransport> _queues = new();

    private readonly IWorldMessageSink _messages;

    private readonly Dictionary<Point2, ChunkAllocationId> _active = [];

    private readonly Dictionary<ChunkAllocationId, ClientChunkSnapshot> _received = [];

    private readonly Dictionary<ChunkAllocationId, DeltaBatch> _deltas = [];

    private readonly LinkedList<ChunkAllocationId> _deltaOrder = [];

    private readonly Dictionary<ChunkAllocationId, long> _resync = [];

    private int _deltaCount;

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    private int _reservedSnapshots;

    private long _transfersCompleted;

    private long _requestsSubmitted;

    public event Action<ChunkAllocationId>? AllocationDiscarded;

    public ClientChunkContentTransport(WorldRuntimeContext runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
        if (runtime.IsAuthority)
        {
            throw new InvalidOperationException("Client content staging requires a client runtime.");
        }

        _runtime = runtime;
        _messages = WorldMessageEndpoint.For(runtime);
        try { _queues.Add(runtime, this); }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("A client world owns exactly one terrain content queue.", exception);
        }

        try
        {
            runtime.OwnWorldResource(this);
            _closed = runtime.Lifetime.Register(ClearAndUnbind);
        }
        catch
        {
            _queues.Remove(runtime);
            _closed.Dispose();
            throw;
        }
    }

    public static ClientChunkContentTransport For(WorldRuntimeContext world)
    {
        return _queues.TryGetValue(world, out var queue)
            ? queue
            : throw new InvalidOperationException("World has no client terrain content queue.");
    }

    public static bool TryGet(WorldRuntimeContext world, [NotNullWhen(true)] out ClientChunkContentTransport? queue)
    {
        return _queues.TryGetValue(world, out queue);
    }

    private void ClearAndUnbind()
    {
        Clear();
        Unbind();
    }

    private void Unbind()
    {
        if (_queues.TryGetValue(_runtime, out var current) && ReferenceEquals(current, this))
        {
            _queues.Remove(_runtime);
        }
    }

    public long PendingSnapshotBytes
    {
        get
        {
            lock (_gate) { return _received.Count * SnapshotBytes; }
        }
    }

    public long BufferedSnapshotBytes
    {
        get
        {
            lock (_gate) { return (_received.Count + _reservedSnapshots) * SnapshotBytes; }
        }
    }

    public long TransfersCompleted => Interlocked.Read(ref _transfersCompleted);

    public long RequestsSubmitted => Interlocked.Read(ref _requestsSubmitted);

    public int PendingDeltaCount
    {
        get
        {
            lock (_gate) { return _deltaCount; }
        }
    }

    public int PendingResyncCount
    {
        get
        {
            lock (_gate) { return _resync.Count; }
        }
    }

    public WorldRuntimeContext Runtime => _runtime;

    public bool IsRequested(ChunkAllocationId allocation)
    {
        lock (_gate)
        {
            return !_disposed &&
                   !_runtime.IsClosed &&
                   _active.TryGetValue(allocation.Coords, out var current) &&
                   current == allocation &&
                   !_received.ContainsKey(allocation);
        }
    }

    public bool CanReceive(ChunkAllocationId allocation, long version)
    {
        lock (_gate)
        {
            return !_disposed &&
                   !_runtime.IsClosed &&
                   _active.TryGetValue(allocation.Coords, out var current) &&
                   current == allocation &&
                   (!_received.TryGetValue(allocation, out var previous) || previous.ContentVersion < version);
        }
    }

    public ClientSnapshotReservation? TryReserve(ChunkAllocationId allocation, Action? abandoned = null)
    {
        lock (_gate)
        {
            if (_disposed ||
                _runtime.IsClosed ||
                !_active.TryGetValue(allocation.Coords, out var current) ||
                current != allocation ||
                (_received.Count + _reservedSnapshots + 1) * SnapshotBytes > MaximumQueuedSnapshotBytes)
            {
                return null;
            }

            _reservedSnapshots++;
            return new ClientSnapshotReservation(this, allocation, abandoned);
        }
    }

    internal void ReleaseReservation()
    {
        lock (_gate) { _reservedSnapshots--; }
    }

    public void Request(IReadOnlyList<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var full = new List<ChunkContentRequest>();
        var discarded = new List<ChunkAllocationId>();
        lock (_gate)
        {
            if (_disposed || _runtime.IsClosed || requests.Count == 0) { return; }

            foreach (var request in requests)
            {
                var allocation = request.Allocation;
                if (_active.TryGetValue(allocation.Coords, out var previous))
                {
                    if (previous.Generation > allocation.Generation) { continue; }

                    if (previous != allocation)
                    {
                        DiscardCore(previous);
                        discarded.Add(previous);
                    }
                }

                _active[allocation.Coords] = allocation;
                if (!_received.ContainsKey(allocation)) { full.Add(request); }
            }

            // Publication only enqueues; no handlers run while allocation ownership is locked.
            foreach (var batch in full.Chunk(TerrainChunksRequestPackage.MaximumRequests))
            {
                NetworkSender.SendToServer(_runtime, new TerrainChunksRequestPackage { ChunkRequests = batch });
                Interlocked.Add(ref _requestsSubmitted, batch.Length);
            }
        }

        foreach (var allocation in discarded) { NotifyDiscard(allocation); }
    }

    public void Discard(ChunkAllocationId allocation)
    {
        lock (_gate) { DiscardCore(allocation); }

        NotifyDiscard(allocation);
    }

    private void NotifyDiscard(ChunkAllocationId allocation)
    {
        AllocationDiscarded?.Invoke(allocation);
        _messages.DiscardQueuedChunkRequests(allocation);
    }

    private void DiscardCore(ChunkAllocationId allocation)
    {
        if (_active.GetValueOrDefault(allocation.Coords) == allocation) { _active.Remove(allocation.Coords); }

        _received.Remove(allocation);
        if (_deltas.Remove(allocation, out var batch))
        {
            _deltaCount -= batch.Values.Count;
            _deltaOrder.Remove(batch.Node);
        }

        _resync.Remove(allocation);
    }

    public void Receive(ClientChunkSnapshot snapshot) => Receive(snapshot, null);

    public void Receive(ClientChunkSnapshot snapshot, ClientSnapshotReservation? reservation)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (reservation != null)
            {
                if (!ReferenceEquals(reservation.Owner, this) || reservation.Allocation != snapshot.Allocation)
                {
                    throw new InvalidOperationException("A snapshot reservation belongs to another content queue.");
                }

                if (!reservation.TryConsume()) { return; }

                _reservedSnapshots--;
            }

            if (_disposed ||
                _runtime.IsClosed ||
                !_active.TryGetValue(snapshot.Allocation.Coords, out var active) ||
                active != snapshot.Allocation ||
                (!_received.ContainsKey(active) &&
                 (_received.Count + _reservedSnapshots + 1) * SnapshotBytes > MaximumQueuedSnapshotBytes)) { return; }

            if (_received.TryGetValue(active, out var previous) && previous.ContentVersion > snapshot.ContentVersion)
            {
                return;
            }

            _received[active] = snapshot;
            Interlocked.Increment(ref _transfersCompleted);
        }
    }

    public void Receive(TerrainCellDelta delta)
    {
        lock (_gate)
        {
            if (_disposed ||
                _runtime.IsClosed ||
                !_active.TryGetValue(Terrain.ToChunk(delta.Cell.X, delta.Cell.Z), out var allocation)) { return; }

            if (_resync.TryGetValue(allocation, out var required))
            {
                _resync[allocation] = Math.Max(required, delta.ResultContentVersion);
                return;
            }

            if (!_deltas.TryGetValue(allocation, out var batch))
            {
                batch = new DeltaBatch(_deltaOrder.AddLast(allocation));
                _deltas.Add(allocation, batch);
            }

            var deltas = batch.Values;
            if (deltas.ContainsKey(delta.BaseContentVersion))
            {
                deltas[delta.BaseContentVersion] = delta;
                return;
            }

            if (_deltaCount == MaximumQueuedDeltas || deltas.Count == MaximumQueuedDeltasPerChunk)
            {
                var floor = Math.Max(
                    delta.ResultContentVersion,
                    deltas.Count == 0 ? 0 : deltas.Values.Max(value => value.ResultContentVersion)
                );
                _deltaCount -= deltas.Count;
                _deltas.Remove(allocation);
                _deltaOrder.Remove(batch.Node);
                _resync[allocation] = floor;
                return;
            }

            deltas.Add(delta.BaseContentVersion, delta);
            _deltaCount++;
        }
    }

    public int DrainReceived(ICollection<ClientChunkSnapshot> destination, int maximumCount = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        lock (_gate)
        {
            var selected = _received.Take(maximumCount).ToArray();
            foreach (var (allocation, snapshot) in selected)
            {
                destination.Add(snapshot);
                _received.Remove(allocation);
            }

            return selected.Length;
        }
    }

    public int DrainDeltas(ICollection<TerrainCellDelta> destination, int maximumCount = MaximumDeltasPerDrain)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        lock (_gate)
        {
            var count = 0;
            while (count < Math.Min(maximumCount, MaximumDeltasPerDrain) && _deltaOrder.First is { } node)
            {
                var deltas = _deltas[node.Value].Values;
                var first = deltas.First();
                destination.Add(first.Value);
                deltas.Remove(first.Key);
                _deltaCount--;
                count++;
                _deltaOrder.Remove(node);
                if (deltas.Count > 0) { _deltaOrder.AddLast(node); }
                else { _deltas.Remove(node.Value); }
            }

            return count;
        }
    }

    public int DrainResyncRequests(ICollection<ChunkContentResync> destination, int maximumCount = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        lock (_gate)
        {
            var selected = _resync.Take(maximumCount).ToArray();
            foreach (var (allocation, version) in selected)
            {
                destination.Add(new ChunkContentResync(allocation, version));
                _resync.Remove(allocation);
            }

            return selected.Length;
        }
    }

    private void Clear()
    {
        lock (_gate)
        {
            _active.Clear();
            _received.Clear();
            _deltas.Clear();
            _deltaOrder.Clear();
            _deltaCount = 0;
            _resync.Clear();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }

            _disposed = true;
            Clear();
        }

        _closed.Dispose();
        Unbind();
    }

    private sealed class DeltaBatch(LinkedListNode<ChunkAllocationId> node)
    {
        internal LinkedListNode<ChunkAllocationId> Node { get; } = node;

        internal SortedDictionary<long, TerrainCellDelta> Values { get; } = [];
    }
}
