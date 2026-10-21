using System.Buffers.Binary;
using System.Reflection;

using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class FrameCodecTest
{
    [Theory]
    [InlineData("connection-request")]
    [InlineData("connection-reject")]
    [InlineData("body-state")]
    [InlineData("terrain-request")]
    [InlineData("terrain-delta")]
    [InlineData("terrain-fragment")]
    [InlineData("initial-snapshot")]
    [InlineData("mod-envelope")]
    public void CapturedFramesPreserveBytes(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".bin"));
        var payload = ProtocolFrameCodec.DecodeFrame(bytes);
        Assert.Equal(0x88, payload[0]);
        using var stream = new MemoryStream(payload);
        Assert.Equal(bytes, ProtocolFrameCodec.EncodeFrame(stream, CompressionPolicy.None));
    }

    [Theory]
    [InlineData(CompressionPolicy.None)]
    [InlineData(CompressionPolicy.Adaptive)]
    [InlineData(CompressionPolicy.Always)]
    public void FramesRoundTripWithoutGameRuntime(CompressionPolicy policy)
    {
        var payload = new byte[16 * 1024];
        using var stream = new MemoryStream(payload);
        var frame = ProtocolFrameCodec.EncodeFrame(stream, policy);
        Assert.Equal(payload, ProtocolFrameCodec.DecodeFrame(frame));
        Assert.Equal(policy == CompressionPolicy.None ? 0 : 1, frame[0]);
    }

    [Fact]
    public void InvalidFrameHeadersAndLengthsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ProtocolFrameCodec.DecodeFrame([0, 0]));
        var frame = new byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), -1);
        Assert.Throws<InvalidDataException>(() => ProtocolFrameCodec.DecodeFrame(frame));
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), ProtocolFrameCodec.MaximumPayloadLength + 1);
        Assert.Throws<InvalidDataException>(() => ProtocolFrameCodec.DecodeFrame(frame));
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), 1);
        Assert.Throws<InvalidDataException>(() => ProtocolFrameCodec.DecodeFrame(frame));
        frame[0] = 255;
        Assert.Throws<InvalidDataException>(() => ProtocolFrameCodec.DecodeFrame(frame));
    }

    [Fact]
    public void BuffersAndGuidsCannotDecodePartialValues()
    {
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, true))
        {
            writer.Write(8);
            writer.Write((byte)1);
        }

        raw.Position = 0;
        using var reader = new ProtocolReader(raw);
        Assert.Throws<InvalidDataException>(() => reader.ReadBuff());
        using var guid = new ProtocolReader(new MemoryStream([2, 0, 0]));
        Assert.Throws<InvalidDataException>(() => guid.ReadGuid());
    }

    [Fact]
    public void VersionAndDependencyBoundaryBelongToProtocolAssembly()
    {
        Assert.Equal(1, GameProtocol.CurrentVersion);
        var assembly = typeof(GameProtocol).Assembly;
        Assert.Equal(
            "1",
            assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "GameProtocolVersion").Value
        );
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference =>
                reference.Name is "Survivalcraft" or "Engine" or "Engine.Media" or "Engine.Network" or "LiteNetLib"
        );
    }
}
