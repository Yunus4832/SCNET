using Game.Messaging;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class MessagePackage : IPackage
{
    private const int _maximumSegmentCount = 64;

    public byte ID => (byte)PackageType.Message;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public GameMessage GameMessage { get; private set; } = GameMessage.System(string.Empty);

    public MessagePackage()
    {
    }

    public MessagePackage(GameMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        GameMessage = message;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(GameMessage.Kind);
        writer.WriteEnum(GameMessage.Channel);
        writer.WriteEnum(GameMessage.Tone);
        writer.WriteEnum(GameMessage.Presentation);
        writer.Write(GameMessage.SenderName);
        if (GameMessage.Content.Segments.Count > _maximumSegmentCount)
        {
            throw new InvalidDataException(
                $"Too many message segments: {GameMessage.Content.Segments.Count}.");
        }

        writer.Write((byte)GameMessage.Content.Segments.Count);
        foreach (var segment in GameMessage.Content.Segments)
        {
            writer.WriteEnum(segment.Style);
            writer.Write(segment.Text);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        var kind = reader.ReadEnum<GameMessageKind>();
        var channel = reader.ReadEnum<GameMessageChannel>();
        var tone = reader.ReadEnum<GameMessageTone>();
        var presentation = reader.ReadEnum<GameMessagePresentation>();
        var senderName = reader.ReadString();
        var segmentCount = reader.ReadByte();
        if (segmentCount > _maximumSegmentCount)
        {
            throw new InvalidDataException($"Too many message segments: {segmentCount}.");
        }

        var segments = new MessageSegment[segmentCount];
        for (var index = 0; index < segmentCount; index++)
        {
            var style = reader.ReadEnum<MessageTextStyle>();
            var text = reader.ReadString();
            segments[index] = new MessageSegment(text, style);
        }

        GameMessage = new GameMessage(
            kind,
            channel,
            senderName,
            new MessageContent(segments),
            tone,
            presentation);
    }
}
