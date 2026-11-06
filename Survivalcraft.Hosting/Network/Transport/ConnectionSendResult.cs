namespace Game.Network.Transport;

public enum ConnectionSendResult
{
    Accepted = 0,

    Backpressured = 1,

    Closed = 2
}
