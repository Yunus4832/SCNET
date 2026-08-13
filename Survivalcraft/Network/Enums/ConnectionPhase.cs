namespace Game.Network.Enums;

public enum ConnectionPhase
{
    TransportConnected,
    BootstrapSent,
    BootstrapApplied,
    WorldSnapshotSent,
    WorldSnapshotApplied,
    Live
}
