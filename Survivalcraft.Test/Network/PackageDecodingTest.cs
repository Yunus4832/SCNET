using System.Net;

using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

[Collection(PackageDecodingCollection.Name)]
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
        Assert.Empty(PackageManager.DecodePackages(null, ReadOnlyMemory<byte>.Empty));
        var package = new ConnectionRejectPackage("world-unavailable");
        PackageManager.RegisterPackage(new ConnectionRejectPackageHandler());
        try
        {
            var writer = ProtocolPacketCodec.Encode([package], CompressionPolicy.None);
            var payload = CommonLib.GetWriter(writer).CopyData();
            var sender = new Client(new IPEndPoint(IPAddress.Loopback, 30000));
            var decoded = PackageManager.DecodePackage<ConnectionRejectPackage>(null, payload, sender);
            Assert.Equal(package.Reason, decoded.Package.Reason);
            Assert.Same(sender, decoded.Context.Sender);
            Assert.Null(decoded.Context.Node);
            Assert.False(decoded.Context.IsServer);
        }
        finally
        {
            PackageManager.UnregisterPackage(package.GetType());
        }
    }

    [Fact]
    public void InvalidBatchDoesNotDeliverItsValidPrefix()
    {
        var package = new ConnectionRejectPackage("valid-prefix");
        PackageManager.RegisterPackage(new ConnectionRejectPackageHandler());
        try
        {
            var writer = ProtocolPacketCodec.Encode([package], CompressionPolicy.None);
            writer = [.. writer, 0x87];
            var bytes = CommonLib.GetWriter(writer).CopyData();
            Assert.Empty(PackageManager.DecodePackages(null, bytes));
        }
        finally
        {
            PackageManager.UnregisterPackage(package.GetType());
        }
    }
}
