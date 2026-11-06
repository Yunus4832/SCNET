using System.Text;

namespace Game.Protocol.Localization;

public static class LocalizedTextLimits
{
    public const int MaximumDepth = 16;

    public const int MaximumNodes = 256;

    public const int MaximumStringBytes = 65536;

    public static void Validate(LocalizedText text)
    {
        var nodes = 0;
        var bytes = 0;
        Visit(text, 0, ref nodes, ref bytes);
    }

    public static long GetRetainedBytes(LocalizedText text)
    {
        Validate(text);
        return Charge(text);
    }

    private static long Charge(LocalizedText text)
    {
        var bytes = checked(128L +
                            2L * (text.Section.Length + text.Key.Length + text.Fallback.Length) +
                            8L * text.Arguments.Count);
        if (text.Template is not null) { bytes = checked(bytes + Charge(text.Template)); }

        foreach (var argument in text.Arguments) { bytes = checked(bytes + Charge(argument)); }

        return bytes;
    }

    private static void Visit(LocalizedText text, int depth, ref int nodes, ref int bytes)
    {
        bytes += Encoding.UTF8.GetByteCount(text.Section) +
                 Encoding.UTF8.GetByteCount(text.Key) +
                 Encoding.UTF8.GetByteCount(text.Fallback);
        if (depth > MaximumDepth || ++nodes > MaximumNodes || bytes > MaximumStringBytes)
        {
            throw new InvalidDataException("Localized text exceeds its limits.");
        }

        if (text.Template is not null) { Visit(text.Template, depth + 1, ref nodes, ref bytes); }

        foreach (var argument in text.Arguments) { Visit(argument, depth + 1, ref nodes, ref bytes); }
    }

    public static LocalizedText Create(
        LocalizedTextKind kind,
        string section,
        string key,
        string fallback,
        LocalizedText? template,
        LocalizedText[] arguments
    )
    {
        return kind switch
        {
            LocalizedTextKind.Literal when section.Length == 0 &&
                                           key.Length == 0 &&
                                           template is null &&
                                           arguments.Length == 0 => LocalizedText.Literal(fallback),
            LocalizedTextKind.Resource when template is null && arguments.Length == 0 => new(section, key, fallback),
            LocalizedTextKind.Database when template is null && arguments.Length == 0 => LocalizedText.Database(
                section,
                key,
                fallback
            ),
            LocalizedTextKind.ContentWidget when template is null && arguments.Length == 0 => LocalizedText
                .ContentWidget(section, key, fallback),
            LocalizedTextKind.Format when section.Length == 0 &&
                                          key.Length == 0 &&
                                          fallback.Length == 0 &&
                                          template is not null => LocalizedText.Format(template, arguments),
            _ => throw new InvalidDataException("Invalid localized text node.")
        };
    }
}
