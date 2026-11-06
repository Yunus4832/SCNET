using Game.Terrains.Distribution;

namespace Game.Network.Chunks;

/// <summary>Adapts owned logical snapshots into bounded native transfers, sharing one encoder and cache per node.</summary>
internal sealed class NativeTerrainDelivery : IDisposable
{
    internal const int maximumActiveTransfersPerClient = 4;

    internal const int maximumActivePayloadBytesPerClient = 4 * 1024 * 1024;

    private readonly NetNode _node;

    private readonly WorldRuntimeContext _runtime;

    private readonly Func<Client, ChunkDeliveryView?> _view;

    private readonly NetworkChunkEncoder _encoder;

    private readonly NetworkChunkCache _cache = new();

    private readonly HashSet<AuthorityChunkDescriptor> _encodes = [];

    private readonly HashSet<AuthorityChunkDescriptor> _invalidatedEncodes = [];

    private readonly Dictionary<Client, PeerTransfers> _peers = new(ReferenceEqualityComparer.Instance);

    private readonly Lock _gate = new();

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    private readonly NetNodeOptions _options;

    internal NativeTerrainDelivery(
        NetNode node,
        Func<Client, ChunkDeliveryView?> view,
        NetNodeOptions options,
        Func<AuthorityChunkSnapshot, CancellationToken, EncodedTerrainChunk>? encode = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _node = node;
        _runtime = node.Runtime;
        if (!_runtime.IsAuthority || _runtime.IsClosed)
        {
            throw new InvalidOperationException("Native terrain delivery requires a live authority runtime.");
        }

        _view = view;
        _encoder = new NetworkChunkEncoder(options.MaximumChunkEncodes, encode);
        _closed = _runtime.Lifetime.Register(Dispose);
        _runtime.OwnWorldResource(this);
    }

    internal int OutstandingEncodes => _encoder.OutstandingCount;

    internal (int Pending, int PinnedBytes) GetBacklog(Client client)
    {
        lock (_gate)
        {
            return _peers.TryGetValue(client, out var peer)
                ? (peer.Active.Count, peer.Active.Values.Sum(value => value.Transmission?.Encoded.Payload.Length ?? 0))
                : (0, 0);
        }
    }

    internal long FragmentsRetransmitted { get; private set; }

    internal long FragmentBytesRetransmitted { get; private set; }

    internal ConnectionSendResult TryAccept(Client client, OutboundPackage package)
    {
        lock (_gate)
        {
            if (_disposed || _runtime.IsClosed || !ReferenceEquals(package.Runtime, _runtime))
            {
                return ConnectionSendResult.Closed;
            }

            var message = (TerrainSnapshotMessage)package.Package;
            var snapshot = message.Snapshot;
            if (_view(client) is not { } view || !view.Contains(snapshot.Allocation.Coords))
            {
                package.Fanout?.Advance(1, 0);
                return ConnectionSendResult.Accepted;
            }

            var peer = GetPeer(client);
            var coords = snapshot.Allocation.Coords;
            if (peer.Active.TryGetValue(coords, out var previous))
            {
                if (snapshot.Allocation.Generation < previous.Allocation.Generation ||
                    (snapshot.Allocation == previous.Allocation &&
                     message.KnownContentVersion < previous.ContentVersion))
                {
                    previous.Transmission?.RequestAll();
                    package.Fanout?.Advance(1, 0);
                    return ConnectionSendResult.Accepted;
                }

                Remove(client, peer, coords);
            }

            if (!MakeRoom(client, peer, coords, view)) { return ConnectionSendResult.Backpressured; }

            peer.Active.Add(coords, new Transfer(snapshot, package.Fanout));
            return ConnectionSendResult.Accepted;
        }
    }

    internal bool RequestMissing(Client client, TerrainChunkFragmentRequest request)
    {
        lock (_gate)
        {
            if (_disposed ||
                _runtime.IsClosed ||
                _view(client) is not { } view ||
                !view.Contains(request.Allocation.Coords)) { return false; }

            var peer = GetPeer(client);
            var coords = request.Allocation.Coords;
            if (peer.Active.TryGetValue(coords, out var active))
            {
                if (request.Allocation.Generation < active.Allocation.Generation ||
                    (request.Allocation == active.Allocation && request.ContentVersion < active.ContentVersion))
                {
                    return true;
                }

                if (request.Allocation == active.Allocation && request.ContentVersion == active.ContentVersion)
                {
                    if (active.Transmission is null) { return true; }

                    return active.Transmission.RequestMissing(request);
                }
            }

            if (!_cache.TryGet(coords, request.ContentVersion, out var encoded)) { return false; }

            var transmission = new ChunkTransmission(encoded, request.Allocation);
            if (!transmission.RequestMissing(request, true)) { return false; }

            if (!MakeRoom(client, peer, coords, view, encoded.Payload.Length, active != null)) { return false; }

            if (active != null) { Remove(client, peer, coords); }

            peer.Active.Add(coords, new Transfer(request.Allocation, request.ContentVersion, transmission));
            return true;
        }
    }

    internal void Update(double now)
    {
        lock (_gate)
        {
            if (_disposed || _runtime.IsClosed) { return; }

            var completed = _encoder.DrainCompleted(
                _cache,
                descriptor =>
                {
                    _encodes.Remove(descriptor);
                    return !_invalidatedEncodes.Remove(descriptor);
                }
            );
            foreach (var (client, peer) in _peers.ToArray())
            {
                if (!client.IsConnected || _view(client) is not { } view)
                {
                    RemoveClient(client);
                    continue;
                }

                foreach (var coords in peer.Active.Keys.Where(coords => !view.Contains(coords)).ToArray())
                {
                    Remove(client, peer, coords);
                }

                peer.Budget.Advance(
                    now,
                    _options.ChunksPerSecond,
                    _options.BytesPerSecond
                );
                foreach (var transfer in peer.Active.Values.OrderBy(value => view.Priority(value.Allocation.Coords))
                             .ToArray())
                {
                    if (!peer.Active.TryGetValue(transfer.Allocation.Coords, out var current) ||
                        !ReferenceEquals(current, transfer)) { continue; }

                    if (transfer.Transmission is null)
                    {
                        var encoded = completed.FirstOrDefault(value => value.Coords == transfer.Allocation.Coords &&
                                                                        value.ContentVersion == transfer.ContentVersion
                        );
                        if (encoded == null)
                        {
                            _cache.TryGet(transfer.Allocation.Coords, transfer.ContentVersion, out encoded);
                        }

                        if (encoded != null)
                        {
                            while (peer.Active.Values.Sum(value => value.Transmission?.Encoded.Payload.Length ?? 0) +
                                   encoded.Payload.Length >
                                   maximumActivePayloadBytesPerClient)
                            {
                                var worst = peer.Active.Values.Where(value => !ReferenceEquals(value, transfer) &&
                                                                              value.Transmission != null
                                ).MaxBy(value => view.Priority(value.Allocation.Coords));
                                if (worst == null ||
                                    view.Priority(worst.Allocation.Coords) <=
                                    view.Priority(transfer.Allocation.Coords)) { break; }

                                Remove(client, peer, worst.Allocation.Coords);
                            }

                            if (peer.Active.Values.Sum(value => value.Transmission?.Encoded.Payload.Length ?? 0) +
                                encoded.Payload.Length >
                                maximumActivePayloadBytesPerClient) { continue; }

                            transfer.Transmission = new ChunkTransmission(encoded, transfer.Allocation);
                            transfer.Transmission.RequestAll();
                            transfer.Snapshot = null;
                        }
                        else if (transfer.Snapshot is { } snapshot)
                        {
                            var descriptor = new AuthorityChunkDescriptor(
                                snapshot.Allocation.Coords,
                                snapshot.ContentVersion
                            );
                            if (_encoder.CanSchedule(descriptor))
                            {
                                if (_encoder.TrySchedule(
                                        new AuthorityChunkSnapshot(
                                            snapshot.Allocation.Coords,
                                            snapshot.ContentVersion,
                                            snapshot.Cells.ToArray(),
                                            snapshot.Shafts.ToArray()
                                        )
                                    )) { _encodes.Add(descriptor); }
                            }

                            continue;
                        }
                    }

                    var transmission = transfer.Transmission!;
                    while (transmission.TrySend(
                               peer.Budget,
                               fragment => _node.QueueTerrainFragment(client, _runtime, fragment, transfer.Fanout),
                               out var retransmitted,
                               out var bytes
                           ))
                    {
                        if (retransmitted)
                        {
                            FragmentsRetransmitted++;
                            FragmentBytesRetransmitted += bytes;
                        }
                    }

                    if (transmission.IsComplete)
                    {
                        peer.Active.Remove(transfer.Allocation.Coords);
                        transfer.Fanout?.Advance(1, 0);
                    }
                }
            }
        }
    }

    private PeerTransfers GetPeer(Client client)
    {
        if (!_peers.TryGetValue(client, out var peer))
        {
            peer = new PeerTransfers();
            _peers.Add(client, peer);
        }

        return peer;
    }

    private bool MakeRoom(
        Client client,
        PeerTransfers peer,
        Point2 coords,
        ChunkDeliveryView view,
        int bytes = 0,
        bool replacing = false
    )
    {
        var candidates = peer.Active.Where(pair => !replacing || pair.Key != coords).ToArray();
        var retainedBytes = candidates.Sum(pair => pair.Value.Transmission?.Encoded.Payload.Length ?? 0);
        var retainedCount = candidates.Length;
        var removals = new List<Point2>();
        foreach (var candidate in candidates.OrderByDescending(pair => view.Priority(pair.Key)))
        {
            if (retainedCount < maximumActiveTransfersPerClient &&
                retainedBytes + bytes <= maximumActivePayloadBytesPerClient) { break; }

            var worst = candidate.Key;
            if (view.Priority(coords) >= view.Priority(worst)) { return false; }

            removals.Add(worst);
            retainedCount--;
            retainedBytes -= candidate.Value.Transmission?.Encoded.Payload.Length ?? 0;
        }

        if (retainedCount >= maximumActiveTransfersPerClient ||
            retainedBytes + bytes > maximumActivePayloadBytesPerClient) { return false; }

        foreach (var removal in removals) { Remove(client, peer, removal); }

        return true;
    }

    private void Remove(Client client, PeerTransfers peer, Point2 coords)
    {
        if (peer.Active.Remove(coords, out var transfer))
        {
            _node.DiscardQueuedChunkFragments(client, transfer.Allocation);
            transfer.Fanout?.Advance(1, 0);
        }
    }

    internal void Trim(Client client)
    {
        lock (_gate)
        {
            if (!_peers.TryGetValue(client, out var peer)) { return; }

            var view = _view(client);
            foreach (var coords in peer.Active.Keys.Where(coords => view is null || !view.Value.Contains(coords))
                         .ToArray())
            {
                Remove(client, peer, coords);
            }
        }
    }

    internal void RemoveChunk(Point2 coords)
    {
        lock (_gate)
        {
            _cache.Remove(coords);
            foreach (var descriptor in _encodes.Where(value => value.Coords == coords))
            {
                _invalidatedEncodes.Add(descriptor);
            }

            foreach (var (client, peer) in _peers) { Remove(client, peer, coords); }
        }
    }

    internal void DiscardOlderAllocations(Client client, ChunkAllocationId allocation)
    {
        lock (_gate)
        {
            if (_peers.TryGetValue(client, out var peer) &&
                peer.Active.TryGetValue(allocation.Coords, out var transfer) &&
                transfer.Allocation.Generation < allocation.Generation)
            {
                Remove(client, peer, allocation.Coords);
            }
        }
    }

    internal void RemoveClient(Client client)
    {
        lock (_gate)
        {
            if (!_peers.Remove(client, out var peer)) { return; }

            foreach (var coords in peer.Active.Keys.ToArray()) { Remove(client, peer, coords); }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }

            _disposed = true;
            foreach (var client in _peers.Keys.ToArray()) { RemoveClient(client); }

            _encoder.Dispose();
            _cache.Clear();
            _encodes.Clear();
            _invalidatedEncodes.Clear();
        }

        _closed.Dispose();
    }

    private sealed class PeerTransfers
    {
        internal Dictionary<Point2, Transfer> Active { get; } = [];

        internal ChunkSendBudget Budget { get; } = new();
    }

    private sealed class Transfer
    {
        internal Transfer(ClientChunkSnapshot snapshot, NetworkMessageFanout? fanout)
        {
            Snapshot = snapshot;
            Allocation = snapshot.Allocation;
            ContentVersion = snapshot.ContentVersion;
            Fanout = fanout;
        }

        internal Transfer(ChunkAllocationId allocation, long contentVersion, ChunkTransmission transmission)
        {
            Allocation = allocation;
            ContentVersion = contentVersion;
            Transmission = transmission;
        }

        internal ChunkAllocationId Allocation { get; }

        internal long ContentVersion { get; }

        internal NetworkMessageFanout? Fanout { get; }

        internal ClientChunkSnapshot? Snapshot { get; set; }

        internal ChunkTransmission? Transmission { get; set; }
    }
}
