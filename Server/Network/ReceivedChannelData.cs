namespace Game.Network;

internal sealed class ReceivedChannelData
{
    public readonly byte ClientId;
    public readonly NetworkChannel Channel;
    public readonly byte[] Data;

    public ReceivedChannelData(byte clientId, NetworkChannel channel, byte[] data)
    {
        ClientId = clientId;
        Channel = channel;
        Data = data;
    }
}
