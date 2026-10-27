using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;

using MessagePack;

namespace Game.Protocol.Serialization;

public static class ProtocolPacketCodec
{
    public const int MaximumMessagesPerPacket = 1024;

    public static byte[] Encode(
        IEnumerable<IProtocolMessage> messages,
        CompressionPolicy compression = CompressionPolicy.Adaptive,
        ProtocolPacketMeasurements? measurements = null
    )
    {
        var batch = messages as IReadOnlyCollection<IProtocolMessage> ?? messages.ToArray();
        if (batch.Count > MaximumMessagesPerPacket) { throw new InvalidDataException("Too many protocol messages."); }

        var start = Stopwatch.GetTimestamp();
        var output = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(output);
        writer.WriteArrayHeader(batch.Count);
        writer.Flush();
        foreach (var message in batch)
        {
            ProtocolMessageValidation.Validate(message);
            var before = output.WrittenCount;
            MessagePackSerializer.Serialize(output, message, ProtocolMessageCodec.Options);
            measurements?.Messages?.Add((message.GetType(), output.WrittenCount - before));
        }

        if (output.WrittenCount > ProtocolFrameCodec.MaximumPayloadLength)
        {
            throw new InvalidDataException("Protocol payload is too large.");
        }

        var serialized = Stopwatch.GetTimestamp();
        MemoryMarshal.TryGetArray(output.WrittenMemory, out var bytes);
        using var stream = new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count, false);
        var result = WrapFrame(ProtocolFrameCodec.EncodeFrame(stream, compression));
        if (measurements is not null)
        {
            measurements.PayloadBytes = output.WrittenCount;
            measurements.SerializationTicks = serialized - start;
            measurements.FramingTicks = Stopwatch.GetTimestamp() - serialized;
        }

        return result;
    }

    public static byte[] WrapFrame(ReadOnlySpan<byte> frame)
    {
        var packet = new byte[checked(frame.Length + sizeof(int))];
        BinaryPrimitives.WriteInt32LittleEndian(packet, frame.Length);
        frame.CopyTo(packet.AsSpan(sizeof(int)));
        return packet;
    }

    public static IReadOnlyList<IProtocolMessage> Decode(ReadOnlyMemory<byte> packet)
    {
        try
        {
            if (packet.Length < sizeof(int) ||
                BinaryPrimitives.ReadInt32LittleEndian(packet.Span) != packet.Length - sizeof(int))
            {
                throw new InvalidDataException("Protocol packet length does not match its payload.");
            }

            var payload = ProtocolFrameCodec.DecodeFrame(packet.Span[sizeof(int)..].ToArray());
            var reader = new MessagePackReader(payload);
            if (reader.ReadArrayHeader() > MaximumMessagesPerPacket)
            {
                throw new InvalidDataException("Too many protocol messages.");
            }

            ProtocolShapeGuard.Validate<IProtocolMessage[]>(payload);
            reader = new MessagePackReader(payload);
            var messages = MessagePackSerializer.Deserialize<IProtocolMessage[]>(
                ref reader,
                ProtocolMessageCodec.Options
            );
            if (messages is null || !reader.End) { throw new InvalidDataException("Invalid protocol batch."); }

            foreach (var message in messages) { ProtocolMessageValidation.Validate(message); }

            return messages;
        }
        catch (Exception exception) when (exception is MessagePackSerializationException or ArgumentException
                                              or EndOfStreamException)
        {
            throw new InvalidDataException("Invalid protocol packet.", exception);
        }
    }
}
