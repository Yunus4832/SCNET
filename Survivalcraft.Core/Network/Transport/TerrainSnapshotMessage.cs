using Game.Terrains.Distribution;

namespace Game.Network.Transport;

/// <summary>A complete, owned logical snapshot; native connections carry encoded fragments instead.</summary>
public sealed record TerrainSnapshotMessage : IProtocolMessage, IDisposable
{
    private ClientSnapshotReservation? _reservation;

    public TerrainSnapshotMessage(ClientChunkSnapshot snapshot, long knownContentVersion = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Allocation.Generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        }

        Snapshot = snapshot;
        ArgumentOutOfRangeException.ThrowIfNegative(knownContentVersion);
        KnownContentVersion = knownContentVersion;
    }

    public ClientChunkSnapshot Snapshot { get; }

    public long KnownContentVersion { get; }

    public ClientSnapshotReservation? Reservation => _reservation;

    public TerrainSnapshotMessage(ClientChunkSnapshot snapshot, ClientSnapshotReservation reservation) : this(snapshot)
    {
        _reservation = reservation;
    }

    public void Dispose() => Interlocked.Exchange(ref _reservation, null)?.Dispose();

    public TerrainSnapshotMessage Capture() => new(
        new ClientChunkSnapshot(
            Snapshot.Allocation,
            Snapshot.ContentVersion,
            Snapshot.Cells.ToArray(),
            Snapshot.Shafts.ToArray()
        ),
        KnownContentVersion
    );
}
