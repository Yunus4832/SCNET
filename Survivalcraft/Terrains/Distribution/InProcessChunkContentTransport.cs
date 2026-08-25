using System.Collections.Concurrent;

namespace Game.Terrains.Distribution;

/// <summary>
/// Local-mode adapter that preserves the same request/snapshot boundary without serialization.
/// </summary>
public sealed class InProcessChunkContentTransport(IChunkContentAuthority authority) : IChunkContentTransport
{
    private readonly IChunkContentAuthority _authority = authority ?? throw new ArgumentNullException(nameof(authority));

    private readonly ConcurrentDictionary<Point2, ClientChunkSnapshot> _received = new();

    public void Request(IReadOnlyList<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        foreach (var request in requests)
        {
            if (!_authority.TryGetSnapshot(request.Allocation.Coords, out var snapshot) ||
                snapshot.ContentVersion <= request.KnownContentVersion)
            {
                continue;
            }

            // The client store owns installed arrays. Copying here establishes the ownership
            // boundary; local zero-copy leases can be introduced later without changing callers.
            _received[request.Allocation.Coords] = new ClientChunkSnapshot(
                request.Allocation,
                snapshot.ContentVersion,
                snapshot.Cells.ToArray(),
                snapshot.Shafts.ToArray());
        }
    }

    public void Discard(Point2 coords) => _received.TryRemove(coords, out _);

    public int DrainReceived(ICollection<ClientChunkSnapshot> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var count = 0;
        foreach (var coords in _received.Keys)
        {
            if (!_received.TryRemove(coords, out var snapshot))
            {
                continue;
            }

            destination.Add(snapshot);
            count++;
        }

        return count;
    }

    public int DrainFailed(ICollection<ChunkAllocationId> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return 0;
    }

    public int DrainDeltas(ICollection<TerrainCellDelta> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return 0;
    }
}
