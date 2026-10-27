using System.Reflection;

using Game.Protocol.Serialization.Formatters;

using MessagePack;
using MessagePack.Resolvers;

namespace Game.Protocol.Serialization;

/// <summary>Strongly typed message encoding; message construction and business dispatch belong to callers.</summary>
public static class ProtocolMessageCodec
{
    internal static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard
        .WithResolver(
            CompositeResolver.Create(
                [
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    MathValueFormatter.Instance,
                    JsonNodeFormatter.Instance,
                    ProtocolValueFormatter.Instance,
                    ProtocolValueFormatter.Instance,
                    ProtocolValueFormatter.Instance,
                    ProtocolValueFormatter.Instance,
                    ProtocolValueFormatter.Instance,
                    ProtocolValueFormatter.Instance
                ],
                [StandardResolver.Instance]
            )
        )
        .WithSecurity(new ProtocolSerializationSecurity());

    public static IReadOnlyList<Type> MessageTypes { get; } = typeof(IProtocolMessage)
        .GetCustomAttributes<UnionAttribute>().Select(attribute => attribute.SubType).ToArray();

    public static bool IsDefined(Type type) => MessageTypes.Contains(type);

    public static byte[] Encode(IProtocolMessage message)
    {
        ProtocolMessageValidation.Validate(message);
        return MessagePackSerializer.Serialize(message, Options);
    }

    public static IProtocolMessage Decode(ReadOnlyMemory<byte> payload)
    {
        try
        {
            ProtocolShapeGuard.Validate<IProtocolMessage>(payload);
            var reader = new MessagePackReader(payload);
            var result = MessagePackSerializer.Deserialize<IProtocolMessage>(ref reader, Options);
            if (!reader.End || result is null) { throw new InvalidDataException("Invalid message payload."); }

            ProtocolMessageValidation.Validate(result);
            return result;
        }
        catch (Exception exception) when (exception is MessagePackSerializationException or ArgumentException
                                              or EndOfStreamException)
        {
            throw new InvalidDataException("Invalid protocol message.", exception);
        }
    }
}
