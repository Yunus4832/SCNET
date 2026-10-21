namespace Game.Protocol.Localization;

/// <summary>
///     A lazily resolved text resource that can be safely stored by long-lived
///     registrations such as widgets, commands and mods.
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
}
