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
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalizedArguments = arguments?.ToArray() ?? [];
        var template = LanguageManager.Get(section, key);
        return GameMessage.System(
                FormatLocalizedText(template, normalizedArguments),
                tone,
                presentation
            ) with
            {
                LocalizationSection = section,
                LocalizationKey = key,
                LocalizationArguments = normalizedArguments
            };
    }

    public static GameMessage ResolveLocalization(this GameMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.LocalizationSection) ||
            string.IsNullOrWhiteSpace(message.LocalizationKey))
        {
            return message;
        }

        var template = LanguageManager.Get(message.LocalizationSection, message.LocalizationKey);
        if (template == message.LocalizationSection || template == message.LocalizationKey)
        {
            return message;
        }

        try
        {
            return message with
            {
                Content = MessageContent.Plain(FormatLocalizedText(template, message.LocalizationArguments))
            };
        }
        catch (FormatException exception)
        {
            Log.Error(
                $"Invalid message localization format " +
                $"{message.LocalizationSection}.{message.LocalizationKey}: {exception}"
            );
            return message;
        }
    }

    private static string FormatLocalizedText(
        string template,
        IReadOnlyList<string> arguments
    )
    {
        return arguments.Count == 0
            ? template
            : string.Format(
                global::System.Globalization.CultureInfo.CurrentCulture,
                template,
                arguments.Cast<object>().ToArray()
            );
    }
}
