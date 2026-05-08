namespace Game.Network;

public interface INetworkMessage
{
    NetworkChannel Channel { get; }
    void Write(INetworkWriter writer);
    void Read(INetworkReader reader);
}
