using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Messaging;

[MessagePackObject]
public sealed record GameMessage
{
    [Key(0)] public GameMessageKind Kind { get; init; }

    [Key(1)] public GameMessageChannel Channel { get; init; }

    [Key(2)] public MessageSender? Sender { get; init; }

    [Key(3)] public MessageContent Content { get; init; }

    [Key(4)] public GameMessageTone Tone { get; init; }

    [Key(5)] public GameMessagePresentation Presentation { get; init; }

    [Key(6)] public string LocalizationSection { get; init; }

    [Key(7)] public string LocalizationKey { get; init; }

    [Key(8), MaxLength(16)] public IReadOnlyList<string> LocalizationArguments { get; init; }

    [SerializationConstructor]
    public GameMessage(
        GameMessageKind Kind,
        GameMessageChannel Channel,
        MessageSender? Sender,
        MessageContent Content,
        GameMessageTone Tone = GameMessageTone.Normal,
        GameMessagePresentation Presentation = GameMessagePresentation.Default,
        string LocalizationSection = "",
        string LocalizationKey = "",
        IReadOnlyList<string>? LocalizationArguments = null
    )
    {
        this.Kind = Kind;
        this.Channel = Channel;
        this.Sender = Sender;
        this.Content = Content;
        this.Tone = Tone;
        this.Presentation = Presentation;
        this.LocalizationSection = LocalizationSection;
        this.LocalizationKey = LocalizationKey;
        this.LocalizationArguments = LocalizationArguments ?? [];
    }

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
