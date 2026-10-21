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
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88);
        var frame = CommonLib.GetWriter(writer, out _).CopyData();
        var padded = new byte[frame.Length + 8];
        frame.CopyTo(padded, 4);
        using var reader = CommonLib.GetReader(padded.AsMemory(4, frame.Length));
        Assert.Equal(0x88, reader.ReadByte());
        Assert.Throws<InvalidDataException>(() => CommonLib.GetReader(padded.AsMemory(4, frame.Length - 1)));
        Assert.Throws<InvalidDataException>(() => CommonLib.GetReader(padded.AsMemory(4, frame.Length + 1)));
        Assert.Throws<InvalidDataException>(() => CommonLib.GetReader(new byte[3]));
    }

    [Fact]
    public void DecodeUsesExplicitSenderAndAcceptsEmptyPayload()
    {
        Assert.Empty(PackageManager.DecodePackages(null, ReadOnlyMemory<byte>.Empty));
        var package = new ConnectionRejectPackage("world-unavailable");
        PackageManager.RegisterPackage(
            () => new ConnectionRejectPackage(string.Empty),
            new ConnectionRejectPackageHandler()
        );
        try
        {
            using var writer = new ProtocolWriter();
            writer.Write((byte)0x88);
            writer.Write(package.ID);
            ProtocolMessageCodec.Write(package, writer);
            var payload = CommonLib.GetWriter(writer, out _).CopyData();
            var sender = new Client(new IPEndPoint(IPAddress.Loopback, 30000));
            var decoded = PackageManager.DecodePackage<ConnectionRejectPackage>(null, payload, sender);
            Assert.Equal(package.Reason, decoded.Package.Reason);
            Assert.Same(sender, decoded.Context.Sender);
            Assert.Null(decoded.Context.Node);
            Assert.False(decoded.Context.IsServer);
        }
        finally
        {
            PackageManager.UnRegisterPackage(package);
        }
    }

    [Fact]
    public void InvalidBatchDoesNotDeliverItsValidPrefix()
    {
        var package = new ConnectionRejectPackage("valid-prefix");
        PackageManager.RegisterPackage(
            () => new ConnectionRejectPackage(string.Empty),
            new ConnectionRejectPackageHandler()
        );
        try
        {
            using var writer = new ProtocolWriter();
            writer.Write((byte)0x88);
            writer.Write(package.ID);
            ProtocolMessageCodec.Write(package, writer);
            writer.Write((byte)0x87);
            var bytes = CommonLib.GetWriter(writer, out _).CopyData();
            Assert.Empty(PackageManager.DecodePackages(null, bytes));
        }
        finally
        {
            PackageManager.UnRegisterPackage(package);
        }
    }
}
