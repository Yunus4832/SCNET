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

        writer.WriteArrayHeader(3);
        writer.Write(value.Section);
        writer.Write(value.Key);
        writer.Write(value.Fallback);
    }

    LocalizedText? IMessagePackFormatter<LocalizedText?>.Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options
    )
    {
        if (reader.TryReadNil()) { return null; }

        if (reader.ReadArrayHeader() != 3)
        {
            throw new MessagePackSerializationException("Invalid localized text length.");
        }

        var section = reader.ReadString() ?? throw new MessagePackSerializationException("Missing text section.");
        var key = reader.ReadString() ?? throw new MessagePackSerializationException("Missing text key.");
        var fallback = reader.ReadString() ?? throw new MessagePackSerializationException("Missing text fallback.");
        return section.Length == 0 && key.Length == 0
            ? LocalizedText.Literal(fallback)
            : new LocalizedText(section, key, fallback);
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
