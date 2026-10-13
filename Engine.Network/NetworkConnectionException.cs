namespace Engine.Network;

public sealed class NetworkConnectionException(NetworkDisconnectInfo information)
    : IOException($"Connection ended: {information.Reason}")
{
    public NetworkDisconnectInfo Information { get; } = information;
}
