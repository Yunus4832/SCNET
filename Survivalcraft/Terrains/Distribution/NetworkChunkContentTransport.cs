using System.Collections.Concurrent;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Game.Terrains.Distribution;

/// <summary>
///     Remote transport boundary for authoritative chunk contents.
///     Network handlers enqueue immutable snapshots; the terrain update loop owns installation.
/// </summary>
public sealed class NetworkChunkContentTransport : IChunkContentTransport
{
    private readonly ConcurrentQueue<ClientChunkSnapshot> _received = new();

    private readonly ConcurrentQueue<ChunkAllocationId> _failed = new();

    private readonly ConcurrentQueue<TerrainCellDelta> _deltas = new();

    private readonly TerrainChunkFragmentReassembler _reassembler = new();

    private long _fragmentsReceived;

    private long _transfersCompleted;

    private long _fullChunkRequestsSent;

    private long _missingFragmentRequestsSent;

    public int PendingTransferCount => _reassembler.PendingCount;

    public long FragmentsReceived => Interlocked.Read(ref _fragmentsReceived);

    public long TransfersCompleted => Interlocked.Read(ref _transfersCompleted);

    public long FullChunkRequestsSent => Interlocked.Read(ref _fullChunkRequestsSent);

    public long MissingFragmentRequestsSent => Interlocked.Read(ref _missingFragmentRequestsSent);

    public void Request(IReadOnlyList<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return;
        }

        _reassembler.DiscardOtherGenerations(requests.Select(request => request.Allocation));

        var fullRequests = new List<ChunkContentRequest>(requests.Count);
        var fragmentRequests = new List<TerrainChunkFragmentRequest>();
        foreach (var request in requests)
        {
            if (_reassembler.TryCreateMissingFragmentRequest(request.Allocation, out var missing))
            {
                fragmentRequests.Add(missing);
            }
            else
            {
                fullRequests.Add(request);
            }
        }

        if (fullRequests.Count > 0)
        {
            Interlocked.Add(ref _fullChunkRequestsSent, fullRequests.Count);
            CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(fullRequests));
        }

        foreach (var batch in fragmentRequests.Chunk(
                     SubsystemTerrainPackage.MaximumFragmentRequestsPerPackage))
        {
            Interlocked.Add(ref _missingFragmentRequestsSent, batch.Length);
            CommonLib.Net.QueuePackage(SubsystemTerrainPackage.CreateFragmentRequest(batch));
        }
    }

    public void Receive(EncodedTerrainChunkFragment fragment)
    {
        Interlocked.Increment(ref _fragmentsReceived);
        if (!_reassembler.Add(fragment, out var encoded))
        {
            return;
        }

        Interlocked.Increment(ref _transfersCompleted);
        _received.Enqueue(NetworkChunkCodec.DecodeSnapshot(
            fragment.Allocation,
            encoded.ContentVersion,
            encoded.Payload));
    }

    public void ReceiveFailures(IEnumerable<ChunkAllocationId> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        foreach (var allocation in allocations)
        {
            _failed.Enqueue(allocation);
        }
    }

    public void Receive(TerrainCellDelta delta) => _deltas.Enqueue(delta);

    public int DrainReceived(ICollection<ClientChunkSnapshot> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var count = 0;
        while (_received.TryDequeue(out var snapshot))
        {
            destination.Add(snapshot);
            count++;
        }

        return count;
    }

    public int DrainFailed(ICollection<ChunkAllocationId> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var count = 0;
        while (_failed.TryDequeue(out var allocation))
        {
            destination.Add(allocation);
            count++;
        }

        return count;
    }

    public int DrainDeltas(ICollection<TerrainCellDelta> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var count = 0;
        while (_deltas.TryDequeue(out var delta))
        {
            destination.Add(delta);
            count++;
        }

        return count;
    }
}
