namespace Game.Protocol.Connection;

public enum ConnectionPhase
{
    TransportConnected,
    BootstrapSent,
    BootstrapApplied,
    WorldSnapshotSent,
    WorldSnapshotApplied,
    Live
}
