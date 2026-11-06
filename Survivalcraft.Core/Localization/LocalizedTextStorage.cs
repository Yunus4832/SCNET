using EntitySystem.TemplatesDatabase;

namespace Game.Localization;

public static class LocalizedTextStorage
{
    public static ValuesDictionary Save(LocalizedText text)
    {
        LocalizedTextLimits.Validate(text);
        return SaveNode(text);
    }

    private static ValuesDictionary SaveNode(LocalizedText text)
    {
        var result = new ValuesDictionary
        {
            { "Kind", text.Kind }, { "Section", text.Section }, { "Key", text.Key }, { "Fallback", text.Fallback }
        };
        if (text.Template is not null) { result.SetValue("Template", SaveNode(text.Template)); }

        var arguments = new ValuesDictionary();
        for (var i = 0; i < text.Arguments.Count; i++)
        {
            arguments.SetValue(
                i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SaveNode(text.Arguments[i])
            );
        }

        result.SetValue("Arguments", arguments);
        return result;
    }

    public static LocalizedText Load(ValuesDictionary values)
    {
        var nodes = 0;
        var text = LoadNode(values, 0, ref nodes);
        LocalizedTextLimits.Validate(text);
        return text;
    }

    private static LocalizedText LoadNode(ValuesDictionary values, int depth, ref int nodes)
    {
        if (depth > LocalizedTextLimits.MaximumDepth || ++nodes > LocalizedTextLimits.MaximumNodes)
        {
            throw new InvalidDataException("Localized text exceeds its limits.");
        }

        var source = values.GetValue<ValuesDictionary>("Arguments");
        if (source.Count > LocalizedTextLimits.MaximumNodes - nodes)
        {
            throw new InvalidDataException("Too many text arguments.");
        }

        var arguments = new LocalizedText[source.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = LoadNode(
                source.GetValue<ValuesDictionary>(i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                depth + 1,
                ref nodes
            );
        }

        var template = values.GetValue<ValuesDictionary?>("Template", null);
        var loadedTemplate = template is null ? null : LoadNode(template, depth + 1, ref nodes);
        return LocalizedTextLimits.Create(
            values.GetValue<LocalizedTextKind>("Kind"),
            values.GetValue<string>("Section"),
            values.GetValue<string>("Key"),
            values.GetValue<string>("Fallback"),
            loadedTemplate,
            arguments
        );
    }
}
