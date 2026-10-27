using Game.Protocol.Serialization;

using MessagePack;

namespace Game.Protocol.Test;

public sealed class PayloadContractTest
{
    [Fact]
    public void NullableCollectionMembersFollowTheDeclaredContract()
    {
        var input = new OptionalValues(["one", null], [1, null]);
        var restored = ProtocolPayloadCodec.Decode<OptionalValues>(ProtocolPayloadCodec.Encode(input));
        Assert.Equal(input.Text, restored.Text);
        Assert.Equal(input.Numbers, restored.Numbers);
    }

    [Fact]
    public void RequiredCollectionMembersCannotBeNull()
    {
        var encoded = ProtocolPayloadCodec.Encode(new OptionalValues(["one", null], [1, 2]));
        Assert.Throws<InvalidDataException>(() => ProtocolPayloadCodec.Decode<RequiredValues>(encoded));
    }

    [Fact]
    public void InvalidUtf8IsRejectedBeforeStringDecoding()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(new byte[] { 0x92, 56, 0x91, 0xa2, 0xc0, 0xaf })
        );
    }

    [MessagePackObject]
    public sealed record OptionalValues(
        [property: Key(0)] IReadOnlyList<string?> Text,
        [property: Key(1)] int?[] Numbers
    );

    [MessagePackObject]
    public sealed record RequiredValues(
        [property: Key(0)] IReadOnlyList<string> Text,
        [property: Key(1)] int[] Numbers
    );
}
