namespace Engine.Network;

public sealed record NetworkDisconnectInfo(
    NetworkDisconnectReason Reason,
    ReadOnlyMemory<byte> Data,
    int SocketErrorCode
);
