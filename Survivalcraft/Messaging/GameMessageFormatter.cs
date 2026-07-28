namespace Game.Messaging;

public static class GameMessageFormatter
{
    public static MessageContent Format(GameMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var segments = new List<MessageSegment>();
        AppendPrefix(segments, message);
        segments.AddRange(message.Content.Segments);
        return new MessageContent(segments);
    }

    private static void AppendPrefix(List<MessageSegment> segments, GameMessage message)
    {
        switch (message.Kind)
        {
            case GameMessageKind.Chat:
                AppendChannelPrefix(segments, message.Channel);
                if (!string.IsNullOrWhiteSpace(message.SenderName))
                {
                    segments.Add(new MessageSegment(
                        $"[{message.SenderName}]",
                        MessageTextStyle.Sender));
                }

                break;
            case GameMessageKind.System:
                segments.Add(new MessageSegment(
                    GetText("SystemPrefix", "[系统]"),
                    ResolveToneStyle(message.Tone, MessageTextStyle.System)));
                break;
            case GameMessageKind.Command:
                segments.Add(new MessageSegment(
                    GetText("CommandPrefix", "[指令]"),
                    ResolveToneStyle(message.Tone, MessageTextStyle.Accent)));
                break;
        }
    }

    private static void AppendChannelPrefix(
        List<MessageSegment> segments,
        GameMessageChannel channel)
    {
        switch (channel)
        {
            case GameMessageChannel.Team:
                segments.Add(new MessageSegment(
                    GetText("TeamPrefix", "[队]"),
                    MessageTextStyle.Team));
                break;
        }
    }

    private static MessageTextStyle ResolveToneStyle(
        GameMessageTone tone,
        MessageTextStyle fallback) =>
        tone switch
        {
            GameMessageTone.Success => MessageTextStyle.Success,
            GameMessageTone.Error => MessageTextStyle.Error,
            GameMessageTone.Warning => MessageTextStyle.Warning,
            _ => fallback
        };

    private static string GetText(string key, string fallback)
    {
        var text = LanguageManager.Get("MultiplayerUI", key);
        return text is "MultiplayerUI" || text == key ? fallback : text;
    }
}
