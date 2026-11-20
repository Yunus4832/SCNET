using System.Net;
using System.Security.Cryptography;

using Engine.Network.Transport;

using Rendezvous.Protocol;

namespace Engine.Network.Test.Transport;

public sealed class RelayPacketLayerTest
{
    [Fact]
    public void RelayRouteAuthenticatesAndRejectsReplayWhileDirectPacketIsUnchanged()
    {
        var layer = new RelayPacketLayer();
        var relay = new IPEndPoint(IPAddress.Loopback, 30000);
        var direct = new IPEndPoint(IPAddress.Loopback, 30001);
        var key = RandomNumberGenerator.GetBytes(32);
        var route = Guid.NewGuid();
        layer.Add(relay, route, 0, key);
        var bytes = new byte[] { 9, 1, 2, 3, 9 };
        var offset = 1;
        var length = 3;
        layer.ProcessOutBoundPacket(direct, ref bytes, ref offset, ref length);
        Assert.Equal(1, offset);
        Assert.Equal(3, length);
        layer.ProcessOutBoundPacket(relay, ref bytes, ref offset, ref length);
        Assert.True(RelayDatagramCodec.TryDecode(bytes, key, out var packet));
        Assert.Equal(new byte[] { 1, 2, 3 }, packet!.Payload);
        var replay = bytes.ToArray();
        layer.ProcessInboundPacket(relay, ref bytes, ref offset, ref length);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal(3, length);
        offset = 0;
        length = replay.Length;
        layer.ProcessInboundPacket(relay, ref replay, ref offset, ref length);
        Assert.Equal(0, length);
        var forged = RelayDatagramCodec.Encode(Guid.NewGuid(), 0, 2, key, new byte[] { 1 });
        length = forged.Length;
        layer.ProcessInboundPacket(relay, ref forged, ref offset, ref length);
        Assert.Equal(0, length);
        layer.Remove(relay);
        var stale = RelayDatagramCodec.Encode(route, 0, 3, key, new byte[] { 1 });
        length = stale.Length;
        layer.ProcessInboundPacket(relay, ref stale, ref offset, ref length);
        Assert.Equal(0, length);
    }
}
