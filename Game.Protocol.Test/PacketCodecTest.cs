using System.Security.Cryptography;

using Game.Protocol.Connection;
using Game.Protocol.Serialization;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class PacketCodecTest
{
    [Theory]
    [InlineData(CompressionPolicy.None)]
    [InlineData(CompressionPolicy.Always)]
    [InlineData(CompressionPolicy.Adaptive)]
    public void FullPacketRoundTripsBatchAndOuterLength(CompressionPolicy compression)
    {
        var messages = new IProtocolMessage[]
            { new ConnectionRejectPackage(new string('x', 1000)), new SubsystemTimePackage(42, 0.5) };
        var packet = ProtocolPacketCodec.Encode(messages, compression);
        Assert.Equal(packet.Length - 4, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(packet));
        var restored = ProtocolPacketCodec.Decode(packet);
        Assert.Equal(messages, restored);
    }

    [Fact]
    public void CapturedInnerFrameGetsTheLittleEndianEnvelope()
    {
        var frame = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "connection-reject.bin"));
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(frame.Length);
            writer.Write(frame);
        }

        Assert.Equal(stream.ToArray(), ProtocolPacketCodec.WrapFrame(frame));
        Assert.Single(ProtocolPacketCodec.Decode(stream.ToArray()));
    }

    [Fact]
    public void InvalidSuffixRejectsTheWholeBatch()
    {
        var good = ProtocolPacketCodec.Encode([new ConnectionRejectPackage("test")], CompressionPolicy.None);
        var payload = ProtocolFrameCodec.DecodeFrame(good[4..]).Concat(new byte[] { 0x12 }).ToArray();
        using var stream = new MemoryStream(payload);
        var malformed = ProtocolPacketCodec.WrapFrame(ProtocolFrameCodec.EncodeFrame(stream, CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Decode(malformed));
        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Decode(good[..^1]));
    }

    [Fact]
    public void EmptyModFingerprintIsTheCanonicalSha256OfNoData()
    {
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant(),
            GameProtocol.EmptyModDataHash
        );
    }
}
