namespace Game.Network;

public interface IChannelFrameBuilder
{
    void BeginFrame();
    void WriteSync(INetworkSync sync, ushort entityId);
    void WriteMessage(NetworkChannel channel, INetworkMessage message);
    byte[] BuildChannel(NetworkChannel channel);
}
