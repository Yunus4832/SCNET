namespace Game.Protocol.Localization;

[MessagePack.MessagePackFormatter(typeof(Serialization.Formatters.ProtocolValueFormatter))]
public sealed record LocalizedText
{
    public static LocalizedText Empty { get; } = Literal(string.Empty);

    public LocalizedTextKind Kind { get; }

    public string Section { get; }

    public string Key { get; }

    public string Fallback { get; }

    public LocalizedText? Template { get; }

    public IReadOnlyList<LocalizedText> Arguments { get; }

    public bool IsLiteral => Kind == LocalizedTextKind.Literal;

    private LocalizedText(
        LocalizedTextKind kind,
        string section,
        string key,
        string fallback,
        LocalizedText? template = null,
        IEnumerable<LocalizedText>? arguments = null
    )
    {
        Kind = kind;
        Section = section;
        Key = key;
        Fallback = fallback;
        Template = template;
        Arguments = Array.AsReadOnly(arguments?.ToArray() ?? []);
        if (Arguments.Any(argument => argument is null))
        {
            throw new ArgumentException("Text arguments cannot be null.");
        }
    }

    public LocalizedText(string section, string key, string fallback)
        : this(LocalizedTextKind.Resource, section, key, fallback ?? throw new ArgumentNullException(nameof(fallback)))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }

    public bool Equals(LocalizedText? other) => other is not null &&
                                                Kind == other.Kind &&
                                                Section == other.Section &&
                                                Key == other.Key &&
                                                Fallback == other.Fallback &&
                                                Equals(Template, other.Template) &&
                                                Arguments.SequenceEqual(other.Arguments);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Section);
        hash.Add(Key);
        hash.Add(Fallback);
        hash.Add(Template);
        foreach (var argument in Arguments) { hash.Add(argument); }

        return hash.ToHashCode();
    }

    public static LocalizedText Literal(string text) => new(
        LocalizedTextKind.Literal,
        "",
        "",
        text ?? throw new ArgumentNullException(nameof(text))
    );

    public static LocalizedText Database(string category, string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(
            LocalizedTextKind.Database,
            category,
            key,
            fallback ?? throw new ArgumentNullException(nameof(fallback))
        );
    }

    public static LocalizedText ContentWidget(string widget, string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widget);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(
            LocalizedTextKind.ContentWidget,
            widget,
            key,
            fallback ?? throw new ArgumentNullException(nameof(fallback))
        );
    }

    public static LocalizedText Format(LocalizedText template, params LocalizedText[] arguments)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new(LocalizedTextKind.Format, "", "", "", template, arguments);
    }
}
