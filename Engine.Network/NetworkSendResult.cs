namespace Engine.Network;

public enum NetworkSendResult
{
    Accepted = 0,

    Closed = 1,

    TooLarge = 2,

    Backpressure = 3
}
