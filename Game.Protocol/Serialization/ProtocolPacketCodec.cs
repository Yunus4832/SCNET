using System.Buffers.Binary;

namespace Game.Protocol.Serialization;

public static class ProtocolPacketCodec
{
    public static byte[] Encode(IEnumerable<IProtocolMessage> messages, CompressionPolicy compression = CompressionPolicy.Adaptive)
    {
        using var writer = new ProtocolWriter();
        foreach (var message in messages)
        { writer.Write((byte)0x88); writer.Write(message.ID); ProtocolMessageCodec.Write(message, writer); }
        return WrapFrame(writer.Data(compression));
    }

    public static byte[] WrapFrame(ReadOnlySpan<byte> frame)
    {
        var packet = new byte[checked(frame.Length + sizeof(int))];
        BinaryPrimitives.WriteInt32LittleEndian(packet, frame.Length);
        frame.CopyTo(packet.AsSpan(sizeof(int)));
        return packet;
    }

    public static ProtocolReader CreateReader(ReadOnlyMemory<byte> packet)
    {
        if (packet.Length < sizeof(int)) { throw new InvalidDataException("Protocol packet is missing its length prefix."); }
        var length = BinaryPrimitives.ReadInt32LittleEndian(packet.Span);
        if (length < 0 || length != packet.Length - sizeof(int)) { throw new InvalidDataException("Protocol packet length does not match its payload."); }
        return new ProtocolReader(packet.Span[sizeof(int)..].ToArray());
    }

    public static IReadOnlyList<IProtocolMessage> Decode(ReadOnlyMemory<byte> packet)
    {
        using var reader = CreateReader(packet);
        var messages = new List<IProtocolMessage>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            if (reader.ReadByte() != 0x88) { throw new InvalidDataException("Invalid protocol message marker."); }
            messages.Add(ProtocolMessageCodec.Read(reader.ReadByte(), reader));
        }
        return messages;
    }
}
