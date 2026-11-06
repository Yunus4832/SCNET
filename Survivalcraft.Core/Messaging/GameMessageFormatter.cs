namespace Game.Messaging;

public sealed class GameMessageFormatter(Localization.LocalizedTextResolver resolver)
{
    public MessageContent Format(GameMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var segments = new List<MessageSegment>();
        AppendPrefix(segments, message);
        segments.AddRange(message.ResolveLocalization(resolver).Content.Segments);
        return new MessageContent(segments);
    }

    private void AppendPrefix(List<MessageSegment> segments, GameMessage message)
    {
        switch (message.Kind)
        {
            case GameMessageKind.Chat:
                AppendChannelPrefix(segments, message.Channel);
                if (message.Sender is { } sender)
                {
                    segments.Add(
                        new MessageSegment(
                            $"[{SenderLabel(sender)}]",
                            MessageTextStyle.Sender
                        )
                    );
                }

                break;
            case GameMessageKind.System:
                segments.Add(
                    new MessageSegment(
                        GetText("SystemPrefix", "[系统]"),
                        ResolveToneStyle(message.Tone, MessageTextStyle.System)
                    )
                );
                break;
            case GameMessageKind.Command:
                segments.Add(
                    new MessageSegment(
                        GetText("CommandPrefix", "[指令]"),
                        ResolveToneStyle(message.Tone, MessageTextStyle.Accent)
                    )
                );
                break;
            case GameMessageKind.Sign:
                break;
        }
    }

    private string SenderLabel(MessageSender sender)
    {
        var address = sender.SubjectAddress.Value[..14] + "…";
        var name = string.IsNullOrWhiteSpace(sender.SubjectName) ? address : $"{sender.SubjectName} · {address}";
        return sender.CharacterId.HasValue && sender.CharacterName.Length > 0
            ? $"{sender.CharacterName} ({name})"
            : name;
    }

    private void AppendChannelPrefix(
        List<MessageSegment> segments,
        GameMessageChannel channel
    )
    {
        switch (channel)
        {
            case GameMessageChannel.Team:
                segments.Add(
                    new MessageSegment(
                        GetText("TeamPrefix", "[队]"),
                        MessageTextStyle.Team
                    )
                );
                break;
        }
    }

    private static MessageTextStyle ResolveToneStyle(GameMessageTone tone, MessageTextStyle fallback)
    {
        return tone switch
        {
            GameMessageTone.Success => MessageTextStyle.Success,
            GameMessageTone.Error => MessageTextStyle.Error,
            GameMessageTone.Warning => MessageTextStyle.Warning,
            _ => fallback
        };
    }

    private string GetText(string key, string fallback)
    {
        return resolver.Get("MultiplayerUI", key, fallback);
    }
}
