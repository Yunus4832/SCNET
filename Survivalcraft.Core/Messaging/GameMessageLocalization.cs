namespace Game.Messaging;

public static class GameMessageLocalization
{
    public static GameMessage System(
        string section,
        string key,
        IReadOnlyList<string>? arguments = null,
        GameMessageTone tone = GameMessageTone.Normal,
        GameMessagePresentation presentation = GameMessagePresentation.Default
    )
    {
        var template = new LocalizedText(section, key, key);
        var text = arguments is { Count: > 0 }
            ? LocalizedText.Format(template, arguments.Select(LocalizedText.Literal).ToArray())
            : template;
        return GameMessage.System([new MessageSegment(text)], tone, presentation);
    }
}
