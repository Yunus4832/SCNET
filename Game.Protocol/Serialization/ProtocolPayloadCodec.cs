using MessagePack;

namespace Game.Protocol.Serialization;

/// <summary>按命令和模组契约处理强类型载荷，不接受运行时类型名。</summary>
public static class ProtocolPayloadCodec
{
    public static byte[] Encode<T>(T value)
    {
        var result = MessagePackSerializer.Serialize(value, ProtocolMessageCodec.Options);
        if (result.Length > ProtocolFrameCodec.MaximumPayloadLength)
        {
            throw new InvalidDataException("Payload is too large.");
        }

        return result;
    }

    public static T Decode<T>(ReadOnlyMemory<byte> payload)
    {
        try
        {
            ProtocolShapeGuard.Validate<T>(payload);
            var reader = new MessagePackReader(payload);
            var value = MessagePackSerializer.Deserialize<T>(ref reader, ProtocolMessageCodec.Options);
            if (!reader.End || value is null) { throw new InvalidDataException("Invalid typed payload."); }

            return value;
        }
        catch (Exception exception) when (exception is MessagePackSerializationException or ArgumentException
                                              or EndOfStreamException)
        {
            throw new InvalidDataException("Invalid typed payload.", exception);
        }
    }
}
