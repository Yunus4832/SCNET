namespace Game.Protocol.Connection;

public enum ConnectionPhase
{
    TransportConnected = 0,

    BootstrapSent = 1,

    BootstrapApplied = 2,

    WorldSnapshotSent = 3,

    WorldSnapshotApplied = 4,

    Live = 5
}
