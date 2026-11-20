using System.Reflection;

using Game.Protocol.Serialization;

using MessagePack;

namespace Game.Protocol.Test;

internal static class WireTestData
{
    public static byte[] ReplaceField(IProtocolMessage message, int field, byte[] replacement)
    {
        var payload = ProtocolMessageCodec.Encode(message);
        var reader = new MessagePackReader(payload);
        Assert.Equal(2, reader.ReadArrayHeader());
        reader.ReadInt32();
        var count = reader.ReadArrayHeader();
        Assert.InRange(field, 0, count - 1);
        for (var i = 0; i < field; i++) { reader.Skip(); }

        var start = checked((int)reader.Consumed);
        reader.Skip();
        var end = checked((int)reader.Consumed);
        return [.. payload.AsSpan(0, start), .. replacement, .. payload.AsSpan(end)];
    }

    public static byte[] OversizedArray(IProtocolMessage message, int field)
    {
        return ReplaceField(message, field, [0xdd, 0x7f, 0xff, 0xff, 0xff]);
    }

    public static int Tag<T>() => typeof(IProtocolMessage).GetCustomAttributes<UnionAttribute>()
        .Single(attribute => attribute.SubType == typeof(T)).Key;
}
