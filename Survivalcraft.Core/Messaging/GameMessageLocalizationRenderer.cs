using Game.Localization;

namespace Game.Messaging;

public static class GameMessageLocalizationRenderer
{
    public static GameMessage ResolveLocalization(this GameMessage message, LocalizedTextResolver resolver)
    {
        return message with
        {
            Content = new MessageContent(
                message.Content.Segments.Select(segment => segment with
                    {
                        Text = LocalizedText.Literal(resolver.Resolve(segment.Text))
                    }
                )
            )
        };
    }
}
