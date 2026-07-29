using System.Globalization;

namespace Game.Localization;

/// <summary>
/// A lazily resolved text resource that can be safely stored by long-lived
/// registrations such as widgets, commands and mods.
/// </summary>
public sealed record LocalizedText
{
    public static LocalizedText Empty { get; } = new(string.Empty);

    public string Section { get; }

    public string Key { get; }

    public string Fallback { get; }

    public bool IsLiteral { get; }

    private LocalizedText(string literal)
    {
        Section = string.Empty;
        Key = string.Empty;
        Fallback = literal;
        IsLiteral = true;
    }

    public LocalizedText(string section, string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Section = section;
        Key = key;
        Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    public static LocalizedText Literal(string text)
    {
        return string.IsNullOrEmpty(text)
            ? Empty
            : new LocalizedText(text);
    }

    public string Resolve(params string[] arguments)
    {
        return IsLiteral
            ? Fallback
            : LocalizationText.Get(Section, Key, Fallback, arguments);
    }
}

/// <summary>
/// Resolves a language resource with a stable fallback for resources supplied
/// by the base game or a mod.
/// </summary>
public static class LocalizationText
{
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
