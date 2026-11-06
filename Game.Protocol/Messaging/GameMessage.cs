using MessagePack;

namespace Game.Protocol.Messaging;

[MessagePackObject]
[method: SerializationConstructor]
public sealed record GameMessage(
    [property: Key(0)] GameMessageKind Kind,
    [property: Key(1)] GameMessageChannel Channel,
    [property: Key(2)] MessageSender? Sender,
    [property: Key(3)] MessageContent Content,
    [property: Key(4)] GameMessageTone Tone = GameMessageTone.Normal,
    [property: Key(5)] GameMessagePresentation Presentation = GameMessagePresentation.Default,
    [property: Key(6)] GameMessageToastStyle? ToastStyle = null
)
{
    public static GameMessage Chat(
        GameMessageChannel channel,
        MessageSender sender,
        string text
    ) =>
        new(GameMessageKind.Chat, channel, sender, MessageContent.Plain(text));

    public static GameMessage System(
        string text,
        GameMessageTone tone = GameMessageTone.Normal,
        GameMessagePresentation presentation = GameMessagePresentation.Default
    ) =>
        new(
            GameMessageKind.System,
            GameMessageChannel.Global,
            null,
            MessageContent.Plain(text),
            tone,
            presentation
        );

    public static GameMessage Sign(IEnumerable<MessageSegment> segments) =>
        new(
            GameMessageKind.Sign,
            GameMessageChannel.Global,
            null,
            new MessageContent(segments),
            Presentation: GameMessagePresentation.Toast
        );

    public static GameMessage System(
        IEnumerable<MessageSegment> segments,
        GameMessageTone tone = GameMessageTone.Normal,
        GameMessagePresentation presentation = GameMessagePresentation.Default
    ) =>
        new(
            GameMessageKind.System,
            GameMessageChannel.Global,
            null,
            new MessageContent(segments),
            tone,
            presentation
        );

    public static GameMessage Command(string text, bool success) =>
        new(
            GameMessageKind.Command,
            GameMessageChannel.Global,
            null,
            MessageContent.Plain(text),
            success ? GameMessageTone.Success : GameMessageTone.Error
        );
}
