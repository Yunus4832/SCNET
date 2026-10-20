namespace Game.Protocol.Messaging;

public sealed record GameMessage(
    GameMessageKind Kind,
    GameMessageChannel Channel,
    string SenderName,
    MessageContent Content,
    GameMessageTone Tone = GameMessageTone.Normal,
    GameMessagePresentation Presentation = GameMessagePresentation.Default)
{
    public string LocalizationSection { get; init; } = string.Empty;

    public string LocalizationKey { get; init; } = string.Empty;

    public IReadOnlyList<string> LocalizationArguments { get; init; } = [];

    public static GameMessage Chat(
        GameMessageChannel channel,
        string senderName,
        string text) =>
        new(GameMessageKind.Chat, channel, senderName, MessageContent.Plain(text));

    public static GameMessage System(
        string text,
        GameMessageTone tone = GameMessageTone.Normal,
        GameMessagePresentation presentation = GameMessagePresentation.Default) =>
        new(
            GameMessageKind.System,
            GameMessageChannel.Global,
            string.Empty,
            MessageContent.Plain(text),
            tone,
            presentation);

    public static GameMessage Sign(IEnumerable<MessageSegment> segments) =>
        new(
            GameMessageKind.Sign,
            GameMessageChannel.Global,
            string.Empty,
            new MessageContent(segments),
            Presentation: GameMessagePresentation.Toast);

    public static GameMessage System(
        IEnumerable<MessageSegment> segments,
        GameMessageTone tone = GameMessageTone.Normal,
        GameMessagePresentation presentation = GameMessagePresentation.Default) =>
        new(
            GameMessageKind.System,
            GameMessageChannel.Global,
            string.Empty,
            new MessageContent(segments),
            tone,
            presentation);

    public static GameMessage Command(string text, bool success) =>
        new(
            GameMessageKind.Command,
            GameMessageChannel.Global,
            string.Empty,
            MessageContent.Plain(text),
            success ? GameMessageTone.Success : GameMessageTone.Error);
}
