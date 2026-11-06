using LiteNetLib.Utils;

namespace Game.Network.Transport;

public static class NetworkWireFraming
{
    public static NetDataWriter Wrap(byte[] packet)
    {
        var writer = new NetDataWriter();
        writer.Put(packet);
        return writer;
    }
}
