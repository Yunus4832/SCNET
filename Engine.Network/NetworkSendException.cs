namespace Engine.Network;

public sealed class NetworkSendException(NetworkSendResult result) : IOException($"Transport send refused: {result}")
{
    public NetworkSendResult Result { get; } = result;
}
