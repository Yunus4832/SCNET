using System.Buffers.Binary;
using System.Reflection;

using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class FrameCodecTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    public void UncompressedFramesPreserveOpaqueBytes(int count)
    {
        var payload = Enumerable.Range(0, count).Select(index => (byte)index).ToArray();
        using var stream = new MemoryStream(payload);
        var frame = ProtocolFrameCodec.EncodeFrame(stream, CompressionPolicy.None);
        Assert.Equal(payload, ProtocolFrameCodec.DecodeFrame(frame));
        Assert.Equal(5 + count, frame.Length);
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
        var message = new Connection.ConnectionPhaseAckPackage(
            Guid.NewGuid(),
            Connection.ConnectionPhase.BootstrapApplied
        );
        var malformed = WireTestData.ReplaceField(message, 0, [0xa3, 1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
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
