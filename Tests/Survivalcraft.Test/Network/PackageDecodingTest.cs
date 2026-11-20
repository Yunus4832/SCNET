using System.Net;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class PackageDecodingTest
{
    [Fact]
    public void FrameReaderRespectsMemorySliceAndRejectsTruncatedOrTrailingBytes()
    {
        var frame = ProtocolPacketCodec.Encode([new ConnectionRejectPackage("test")]);
        var padded = new byte[frame.Length + 8];
        frame.CopyTo(padded, 4);
        Assert.Single(ProtocolPacketCodec.Decode(padded.AsMemory(4, frame.Length)));
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Decode(padded.AsMemory(4, frame.Length - 1)));
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Decode(padded.AsMemory(4, frame.Length + 1)));
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Decode(new byte[3]));
    }

    [Fact]
    public void DecodeUsesExplicitSenderAndAcceptsEmptyPayload()
    {
        var sender = new Client(new IPEndPoint(IPAddress.Loopback, 30000));
        var context = PackageReceiveContext.ForDiscovery(PackageHandlerCatalog.Empty, sender);
        try
        {
            Assert.Empty(ProtocolReceivePolicy.DecodePackages(context, ReadOnlyMemory<byte>.Empty));
            var package = new ConnectionRejectPackage("world-unavailable");
            var frame = ProtocolPacketCodec.Encode([package], CompressionPolicy.None);
            var decoded = ProtocolReceivePolicy.DecodePackage<ConnectionRejectPackage>(
                context,
                NetworkWireFraming.Wrap(frame).CopyData()
            );
            Assert.Equal(package.Reason, decoded.Package.Reason);
            Assert.Same(sender, decoded.Context.Sender);
            Assert.Null(decoded.Context.Node);
            Assert.False(decoded.Context.IsServer);
        }
        finally { context.Runtime.Close(); }
    }

    [Fact]
    public void InvalidBatchDoesNotDeliverItsValidPrefix()
    {
        var context = PackageReceiveContext.ForDiscovery(PackageHandlerCatalog.Empty);
        try
        {
            var frame = ProtocolPacketCodec.Encode(
                [new ConnectionRejectPackage("valid-prefix")],
                CompressionPolicy.None
            );
            Assert.Empty(
                ProtocolReceivePolicy.DecodePackages(context, NetworkWireFraming.Wrap([.. frame, 0x87]).CopyData())
            );
        }
        finally { context.Runtime.Close(); }
    }
}
