using System.Globalization;

namespace Game.Localization;

public static class LocalizationText
{
    public static string Resolve(this LocalizedText text, params string[] arguments)
    {
        return text.IsLiteral
            ? text.Fallback
            : Get(text.Section, text.Key, text.Fallback, arguments);
    }

    public static string Get(
        string section,
        string key,
        string fallback,
        params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var template = LanguageManager.Get(section, key);
        if (template == section || template == key)
        {
            template = fallback;
        }

        if (arguments.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException exception)
        {
            Log.Error($"Invalid localization format {section}.{key}: {exception}");
            return string.Format(CultureInfo.InvariantCulture, fallback, arguments);
        }
    }
}
