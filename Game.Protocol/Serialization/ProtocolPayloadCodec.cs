using MessagePack;

namespace Game.Protocol.Serialization;

/// <summary>Typed payloads owned by commands and mod contracts; no runtime type names are accepted.</summary>
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
