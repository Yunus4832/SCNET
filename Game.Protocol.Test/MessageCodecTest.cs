using System.Reflection;

using Game.Protocol.Connection;
using Game.Protocol.Serialization;

using MessagePack;

namespace Game.Protocol.Test;

public sealed class MessageCodecTest
{
    [Fact]
    public void RejectionMatchesAnIndependentMessagePackSample()
    {
        // [56, ["拒绝 test"]]: numeric union tag and a single required string field.
        byte[] sample = [0x92, 56, 0x91, 0xab, 0xe6, 0x8b, 0x92, 0xe7, 0xbb, 0x9d, 0x20, 0x74, 0x65, 0x73, 0x74];
        var message = Assert.IsType<ConnectionRejectPackage>(ProtocolMessageCodec.Decode(sample));
        Assert.Equal("拒绝 test", message.Reason);
        Assert.Equal(sample, ProtocolMessageCodec.Encode(message));
    }

    [Fact]
    public void UnionTagsAndConcreteContractsAreUnique()
    {
        var variants = typeof(IProtocolMessage).GetCustomAttributes<UnionAttribute>().ToArray();
        Assert.Equal(variants.Length, variants.Select(value => value.Key).Distinct().Count());
        Assert.Equal(variants.Length, variants.Select(value => value.SubType).Distinct().Count());
        Assert.All(
            variants,
            value =>
            {
                Assert.False(value.SubType.IsAbstract);
                Assert.True(ProtocolMessageCodec.IsDefined(value.SubType));
            }
        );
    }

    [Theory]
    [InlineData(new byte[] { 0x92, 0xcd, 0x7f, 0xff, 0x90 })]
    [InlineData(new byte[] { 0x92, 56, 0x90 })]
    [InlineData(new byte[] { 0x92, 56, 0x91, 0xc0 })]
    [InlineData(new byte[] { 0x92, 56, 0x91, 0xa5, 1 })]
    [InlineData(new byte[] { 0x92, 56, 0x91, 0xa0, 0x00 })]
    public void UnknownVariantsMissingFieldsNullsTruncationAndTrailingBytesAreRejected(byte[] payload)
    {
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(payload));
    }
}
