namespace Engine.Network;

public enum NetworkDisconnectReason
{
    Failed = 0,

    Timeout = 1,

    Unreachable = 2,

    RemoteClose = 3,

    LocalClose = 4,

    Rejected = 5,

    ProtocolError = 6,

    Interrupted = 7,

    Unknown = 8
}
