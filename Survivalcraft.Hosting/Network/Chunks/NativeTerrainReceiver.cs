using Game.Terrains.Distribution;

namespace Game.Network.Chunks;

/// <summary>Native-only fragment recovery and decoding; complete messages reserve capacity before entering the inbox.</summary>
internal sealed class NativeTerrainReceiver : IDisposable
{
    private readonly WorldRuntimeContext _runtime;

    private readonly ClientChunkContentTransport _content;

    private readonly CancellationToken _connectionClosed;

    private readonly CancellationTokenRegistration _worldClosed;

    private readonly CancellationTokenRegistration _linkClosed;

    private readonly TerrainChunkFragmentReassembler _reassembler = new();

    private readonly Lock _gate = new();

    private bool _disposed;

    internal NativeTerrainReceiver(
        WorldRuntimeContext runtime,
        ClientChunkContentTransport content,
        CancellationToken connectionClosed = default
    )
    {
        if (runtime.IsAuthority || !ReferenceEquals(content.Runtime, runtime))
        {
            throw new InvalidOperationException(
                "Native terrain reception requires the content queue of its client runtime."
            );
        }

        ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
        _runtime = runtime;
        _content = content;
        _connectionClosed = connectionClosed;
        _content.AllocationDiscarded += Reject;
        _worldClosed = runtime.Lifetime.Register(Clear);
        _linkClosed = connectionClosed.Register(Clear);
        try { runtime.OwnWorldResource(this); }
        catch
        {
            Dispose();
            throw;
        }
    }

    private bool IsClosed => _disposed || _runtime.IsClosed || _connectionClosed.IsCancellationRequested;

    internal int PendingTransferCount => _reassembler.PendingCount;

    internal long PendingTransferBytes => _reassembler.ReservedBytes;

    internal long FragmentsReceived { get; private set; }

    internal long SnapshotsDecoded { get; private set; }

    internal long FullChunkRequestsSent { get; private set; }

    internal long MissingFragmentRequestsSent { get; private set; }

    internal IReadOnlyList<IProtocolMessage> AdaptRequests(TerrainChunksRequestPackage request)
    {
        lock (_gate)
        {
            if (IsClosed) { return []; }

            var full = new List<ChunkContentRequest>();
            var repairs = new List<TerrainChunkFragmentRequest>();
            foreach (var chunk in request.ChunkRequests)
            {
                if (!_content.IsRequested(chunk.Allocation)) { continue; }

                if (_reassembler.TryCreateMissingFragmentRequest(chunk.Allocation, out var missing))
                {
                    repairs.Add(missing);
                }
                else { full.Add(chunk); }
            }

            var messages = new List<IProtocolMessage>();
            if (full.Count > 0) { messages.Add(new TerrainChunksRequestPackage { ChunkRequests = full.ToArray() }); }

            foreach (var repair in repairs) { messages.Add(SubsystemTerrainPackage.CreateFragmentRequest([repair])); }

            FullChunkRequestsSent += full.Count;
            MissingFragmentRequestsSent += repairs.Count;
            return messages;
        }
    }

    internal TerrainSnapshotMessage? Receive(EncodedTerrainChunkFragment fragment)
    {
        lock (_gate)
        {
            if (IsClosed || !_content.CanReceive(fragment.Allocation, fragment.ContentVersion)) { return null; }

            FragmentsReceived++;
            if (!_reassembler.Add(fragment, out var encoded)) { return null; }

            var reservation = _content.TryReserve(fragment.Allocation, () => Reject(fragment.Allocation));
            if (reservation == null)
            {
                _reassembler.Discard(fragment.Allocation);
                return null;
            }

            try
            {
                var snapshot = NetworkChunkCodec.DecodeSnapshot(
                    fragment.Allocation,
                    encoded.ContentVersion,
                    encoded.Payload
                );
                if (IsClosed || !_content.CanReceive(fragment.Allocation, fragment.ContentVersion))
                {
                    reservation.Dispose();
                    return null;
                }

                SnapshotsDecoded++;
                return new TerrainSnapshotMessage(snapshot, reservation);
            }
            catch
            {
                reservation.Dispose();
                throw;
            }
        }
    }

    internal void Reject(ChunkAllocationId allocation)
    {
        lock (_gate) { _reassembler.Discard(allocation); }
    }

    private void Clear()
    {
        lock (_gate) { _reassembler.Clear(); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }

            _disposed = true;
            _reassembler.Clear();
        }

        _content.AllocationDiscarded -= Reject;
        _worldClosed.Dispose();
        _linkClosed.Dispose();
    }
}
