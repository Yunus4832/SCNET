using System.Globalization;

namespace Game.Localization;

public sealed class LocalizedTextResolver(LanguageCatalog catalog)
{
    public string Resolve(LocalizedText text, params string[] arguments)
    {
        LocalizedTextLimits.Validate(text);
        var resolved = ResolveNode(text);
        return arguments.Length == 0 ? resolved : string.Format(CultureInfo.CurrentCulture, resolved, arguments);
    }

    private string ResolveNode(LocalizedText text)
    {
        return text.Kind switch
        {
            LocalizedTextKind.Literal => text.Fallback,
            LocalizedTextKind.Resource => Get(text.Section, text.Key, text.Fallback),
            LocalizedTextKind.Database => ResolveDatabase(text),
            LocalizedTextKind.ContentWidget => ResolveContentWidget(text),
            LocalizedTextKind.Format => ResolveFormat(text),
            _ => throw new InvalidDataException("Invalid localized text kind.")
        };
    }

    private string ResolveFormat(LocalizedText text)
    {
        var arguments = text.Arguments.Select(ResolveNode).Cast<object>().ToArray();
        try { return string.Format(CultureInfo.CurrentCulture, ResolveNode(text.Template!), arguments); }
        catch (FormatException error)
        {
            Log.Error($"Invalid localized text format: {error}");
            return string.Format(CultureInfo.InvariantCulture, text.Template!.Fallback, arguments);
        }
    }

    private string ResolveContentWidget(LocalizedText text)
    {
        return catalog.TryGet(out var resolved, "ContentWidgets", text.Section, text.Key) ? resolved : text.Fallback;
    }

    private string ResolveDatabase(LocalizedText text)
    {
        return catalog.TryGet(out var resolved, "Database", text.Section, text.Key) ? resolved : text.Fallback;
    }

    public string Get(
        string section,
        string key,
        string fallback,
        params string[] arguments
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!catalog.TryGet(out var template, section, key))
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
