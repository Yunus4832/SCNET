using Engine.Core.Security;

using Game.Protocol.Identity;
using Game.Protocol.Localization;
using Game.Protocol.Messaging;
using Game.Protocol.Terrain;

using MessagePack;
using MessagePack.Formatters;

namespace Game.Protocol.Serialization.Formatters;

public sealed class ProtocolValueFormatter :
    IMessagePackFormatter<PlayerAddress>, IMessagePackFormatter<ResourceId>, IMessagePackFormatter<ModId>,
    IMessagePackFormatter<LocalizedText?>, IMessagePackFormatter<MessageContent?>, IMessagePackFormatter<CellFace>
{
    public static readonly ProtocolValueFormatter Instance = new();

    void IMessagePackFormatter<PlayerAddress>.Serialize(
        ref MessagePackWriter writer,
        PlayerAddress value,
        MessagePackSerializerOptions options
    ) => writer.Write(value.Value);

    PlayerAddress IMessagePackFormatter<PlayerAddress>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        var value = reader.ReadString();
        if (value?.Length != IdentityAddress.EncodedLength)
        {
            throw new MessagePackSerializationException("Invalid subject address length.");
        }

        return new PlayerAddress(value);
    }

    void IMessagePackFormatter<ResourceId>.Serialize(
        ref MessagePackWriter writer,
        ResourceId value,
        MessagePackSerializerOptions options
    )
    {
        var text = value.ToString();
        if (text.Length > ProtocolLimits.MaximumResourceIdLength)
        {
            throw new MessagePackSerializationException("Resource ID is too long.");
        }

        writer.Write(text);
    }

    ResourceId IMessagePackFormatter<ResourceId>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        var value = reader.ReadString();
        if (value is null || value.Length > ProtocolLimits.MaximumResourceIdLength)
        {
            throw new MessagePackSerializationException("Invalid resource ID length.");
        }

        return ResourceId.Parse(value);
    }

    void IMessagePackFormatter<ModId>.Serialize(
        ref MessagePackWriter writer,
        ModId value,
        MessagePackSerializerOptions options
    ) => writer.Write(value.Value);

    ModId IMessagePackFormatter<ModId>.
        Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => new(reader.ReadString()!);

    void IMessagePackFormatter<LocalizedText?>.Serialize(
        ref MessagePackWriter writer,
        LocalizedText? value,
        MessagePackSerializerOptions options
    )
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        LocalizedTextLimits.Validate(value);
        WriteText(ref writer, value);
    }

    private static void WriteText(ref MessagePackWriter writer, LocalizedText value)
    {
        writer.WriteArrayHeader(6);
        writer.Write((int)value.Kind);
        writer.Write(value.Section);
        writer.Write(value.Key);
        writer.Write(value.Fallback);
        if (value.Template is null) { writer.WriteNil(); }
        else { WriteText(ref writer, value.Template); }

        writer.WriteArrayHeader(value.Arguments.Count);
        foreach (var argument in value.Arguments) { WriteText(ref writer, argument); }
    }

    LocalizedText? IMessagePackFormatter<LocalizedText?>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.TryReadNil()) { return null; }

        var nodes = 0;
        var bytes = 0;
        return ReadText(ref reader, 0, ref nodes, ref bytes);
    }

    private static LocalizedText ReadText(ref MessagePackReader reader, int depth, ref int nodes, ref int bytes)
    {
        if (depth > LocalizedTextLimits.MaximumDepth ||
            ++nodes > LocalizedTextLimits.MaximumNodes ||
            reader.ReadArrayHeader() != 6)
        {
            throw new MessagePackSerializationException("Invalid localized text tree.");
        }

        var kind = (LocalizedTextKind)reader.ReadInt32();
        var section = ReadTextString(ref reader, ref bytes);
        var key = ReadTextString(ref reader, ref bytes);
        var fallback = ReadTextString(ref reader, ref bytes);
        var template = reader.TryReadNil() ? null : ReadText(ref reader, depth + 1, ref nodes, ref bytes);
        var count = reader.ReadArrayHeader();
        if (count > LocalizedTextLimits.MaximumNodes - nodes)
        {
            throw new MessagePackSerializationException("Too many text arguments.");
        }

        var arguments = new LocalizedText[count];
        for (var i = 0; i < count; i++) { arguments[i] = ReadText(ref reader, depth + 1, ref nodes, ref bytes); }

        return LocalizedTextLimits.Create(kind, section, key, fallback, template, arguments);
    }

    private static string ReadTextString(ref MessagePackReader reader, ref int bytes)
    {
        var sequence = reader.ReadStringSequence() ??
                       throw new MessagePackSerializationException("Missing text value.");
        if (sequence.Length > LocalizedTextLimits.MaximumStringBytes - bytes)
        {
            throw new MessagePackSerializationException("Text is too large.");
        }

        var value = System.Text.Encoding.UTF8.GetString(System.Buffers.BuffersExtensions.ToArray(sequence));
        bytes += checked((int)sequence.Length);
        if (bytes > LocalizedTextLimits.MaximumStringBytes)
        {
            throw new MessagePackSerializationException("Text is too large.");
        }

        return value;
    }

    void IMessagePackFormatter<MessageContent?>.Serialize(
        ref MessagePackWriter writer,
        MessageContent? value,
        MessagePackSerializerOptions options
    )
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        if (value.Segments.Count > 64) { throw new MessagePackSerializationException("Too many message segments."); }

        MessagePackSerializer.Serialize(ref writer, value.Segments, options);
    }

    MessageContent? IMessagePackFormatter<MessageContent?>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.TryReadNil()) { return null; }

        var count = reader.ReadArrayHeader();
        if (count > 64) { throw new MessagePackSerializationException("Too many message segments."); }

        var result = new MessageSegment[count];
        options.Security.DepthStep(ref reader);
        try
        {
            for (var i = 0; i < count; i++)
            {
                result[i] = MessagePackSerializer.Deserialize<MessageSegment>(ref reader, options) ??
                            throw new MessagePackSerializationException("Missing message segment.");
            }
        }
        finally { reader.Depth--; }

        return new MessageContent(result);
    }

    void IMessagePackFormatter<CellFace>.Serialize(
        ref MessagePackWriter writer,
        CellFace value,
        MessagePackSerializerOptions options
    )
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.Face);
    }

    CellFace IMessagePackFormatter<CellFace>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.ReadArrayHeader() != 4) { throw new MessagePackSerializationException("Invalid cell face length."); }

        return new CellFace(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }
}
