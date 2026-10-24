using Engine.Core.Mathematics;

using Game.Protocol.Messaging;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private const int _maximumLocalizationArgumentCount = 16;

    private const int _maximumSegmentCount = 64;

    private static void WriteMessagePackage(MessagePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.GameMessage.Kind);
        writer.WriteEnum(message.GameMessage.Channel);
        writer.WriteEnum(message.GameMessage.Tone);
        writer.WriteEnum(message.GameMessage.Presentation);
        var sender = message.GameMessage.Sender;
        if ((message.GameMessage.Kind == GameMessageKind.Chat) != (sender is not null))
        {
            throw new InvalidDataException("Only chat messages require an authenticated sender.");
        }

        writer.Write(sender is not null);
        if (sender is not null)
        {
            PlayerAddressCodec.Write(writer, sender.SubjectAddress);
            writer.Write(sender.SubjectName);
            writer.Write(sender.CharacterId.HasValue);
            if (sender.CharacterId is { } characterId)
            {
                if (characterId == Guid.Empty) { throw new InvalidDataException("Invalid character context."); }

                writer.Write(characterId);
            }
            else if (sender.CharacterName.Length > 0)
            {
                throw new InvalidDataException("A character name requires character context.");
            }

            writer.Write(sender.CharacterName);
        }

        if (message.GameMessage.Content.Segments.Count > _maximumSegmentCount)
        {
            throw new InvalidDataException($"Too many message segments: {message.GameMessage.Content.Segments.Count}.");
        }

        writer.Write((byte)message.GameMessage.Content.Segments.Count);
        foreach (var segment in message.GameMessage.Content.Segments)
        {
            writer.WriteEnum(segment.Style);
            writer.Write(segment.Color.HasValue);
            if (segment.Color.HasValue)
            {
                writer.Write(segment.Color.Value);
            }

            writer.Write(segment.Text);
        }

        writer.Write(message.GameMessage.LocalizationSection);
        writer.Write(message.GameMessage.LocalizationKey);
        if (message.GameMessage.LocalizationArguments.Count > _maximumLocalizationArgumentCount)
        {
            throw new InvalidDataException("Too many message localization arguments.");
        }

        writer.Write((byte)message.GameMessage.LocalizationArguments.Count);
        foreach (var argument in message.GameMessage.LocalizationArguments)
        {
            writer.Write(argument);
        }
    }

    private static MessagePackage ReadMessagePackage(ProtocolReader reader)
    {
        var kind = reader.ReadEnum<GameMessageKind>();
        var channel = reader.ReadEnum<GameMessageChannel>();
        var tone = reader.ReadEnum<GameMessageTone>();
        var presentation = reader.ReadEnum<GameMessagePresentation>();
        MessageSender? sender = null;
        var hasSender = reader.ReadBoolean();
        if ((kind == GameMessageKind.Chat) != hasSender)
        {
            throw new InvalidDataException("Invalid chat sender context.");
        }

        if (hasSender)
        {
            var address = PlayerAddressCodec.Read(reader);
            var subjectName = reader.ReadString();
            Guid? characterId = reader.ReadBoolean() ? reader.ReadGuid() : null;
            var characterName = reader.ReadString();
            if (characterId == Guid.Empty || characterId is null && characterName.Length > 0)
            {
                throw new InvalidDataException("Invalid character context.");
            }

            sender = new MessageSender(address, subjectName, characterId, characterName);
        }

        var segmentCount = reader.ReadByte();
        if (segmentCount > _maximumSegmentCount)
        {
            throw new InvalidDataException($"Too many message segments: {segmentCount}.");
        }

        var segments = new MessageSegment[segmentCount];
        for (var index = 0; index < segmentCount; index++)
        {
            var style = reader.ReadEnum<MessageTextStyle>();
            Color? color = reader.ReadBoolean() ? reader.ReadColor() : null;
            var text = reader.ReadString();
            segments[index] = new MessageSegment(text, style, color);
        }

        var localizationSection = reader.ReadString();
        var localizationKey = reader.ReadString();
        var localizationArgumentCount = reader.ReadByte();
        if (localizationArgumentCount > _maximumLocalizationArgumentCount)
        {
            throw new InvalidDataException("Too many message localization arguments.");
        }

        var localizationArguments = new string[localizationArgumentCount];
        for (var index = 0; index < localizationArgumentCount; index++)
        {
            localizationArguments[index] = reader.ReadString();
        }

        var message = new GameMessage(
            kind,
            channel,
            sender,
            new MessageContent(segments),
            tone,
            presentation
        )
        {
            LocalizationSection = localizationSection,
            LocalizationKey = localizationKey,
            LocalizationArguments = localizationArguments
        };
        return new MessagePackage(message);
    }
}
