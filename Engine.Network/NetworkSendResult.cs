namespace Engine.Network;

public enum NetworkSendResult
{
    Accepted,
    Closed,
    TooLarge,
    Backpressure
}
