namespace Game.Network;

internal sealed class NetworkClient
{
    public readonly byte Id;
    public string Name = string.Empty;

    public NetworkClient(byte id)
    {
        Id = id;
    }
}
