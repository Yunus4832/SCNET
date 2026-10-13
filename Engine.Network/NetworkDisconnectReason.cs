namespace Engine.Network;

public enum NetworkDisconnectReason
{
    Failed,
    Timeout,
    Unreachable,
    RemoteClose,
    LocalClose,
    Rejected,
    ProtocolError,
    Interrupted,
    Unknown
}
