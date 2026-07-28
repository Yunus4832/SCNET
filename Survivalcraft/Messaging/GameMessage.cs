namespace Game.Messaging;

public enum GameMessageKind : byte
{
    Chat,
    System,
    Command
}

public enum GameMessageChannel : byte
{
    Global,
    Team
}

public enum GameMessageTone : byte
{
    Normal,
    Success,
    Error,
    Warning
}

[Flags]
public enum GameMessagePresentation : byte
{
    None = 0,
    History = 1,
    Overlay = 2,
    Toast = 4,
    Default = History | Overlay
}

public enum MessageTextStyle : byte
{
    Normal,
    Sender,
    System,
    Team,
    Success,
    Error,
    Warning,
    Accent
}

public sealed record MessageSegment(
    string Text,
    MessageTextStyle Style = MessageTextStyle.Normal);

public sealed class MessageContent
{
    private readonly MessageSegment[] _segments;

    public IReadOnlyList<MessageSegment> Segments => _segments;

    public string PlainText => string.Concat(_segments.Select(segment => segment.Text));

    public MessageContent(IEnumerable<MessageSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var normalized = new List<MessageSegment>();
        foreach (var segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            if (string.IsNullOrEmpty(segment.Text))
            {
                continue;
            }

            if (normalized.LastOrDefault() is { } previous &&
                previous.Style == segment.Style)
            {
                normalized[^1] = previous with { Text = previous.Text + segment.Text };
            }
            else
            {
                normalized.Add(segment);
            }
        }

        _segments = normalized.ToArray();
    }

    public static MessageContent Plain(string text) =>
        new([new MessageSegment(text ?? string.Empty)]);

}

public sealed record GameMessage(
    GameMessageKind Kind,
    GameMessageChannel Channel,
    string SenderName,
    MessageContent Content,
    GameMessageTone Tone = GameMessageTone.Normal,
    GameMessagePresentation Presentation = GameMessagePresentation.Default)
{
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
