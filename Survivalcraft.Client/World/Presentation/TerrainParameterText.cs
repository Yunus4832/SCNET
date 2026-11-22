using System.Globalization;

using Game.Application;
using Game.Terrains.Generation;

namespace Game.World.Presentation;

public static class TerrainParameterText
{
    public static string Format(TerrainGeneratorParameter parameter, object value)
    {
        if (value is double number)
        {
            var formatted = number.ToString("0.##", CultureInfo.CurrentCulture);
            return parameter.UnitFormatKey == null
                ? formatted
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Localization.ClientLanguage.Catalog.Get("TerrainGeneratorParameters", parameter.UnitFormatKey),
                    formatted
                );
        }

        if (value is bool enabled) { return Localization.ClientLanguage.Catalog.Get("Usual", enabled ? "yes" : "no"); }

        var text = (string)value;
        if (parameter.UsesBlockNames)
        {
            var catalog = ClientApplication.Content.PreviewCatalog;
            var id = ResourceId.Parse(text);
            if (catalog.Rules.Registry.TryGet(id, out var entry))
            {
                return catalog.Get(id).GetDisplayName(null, Terrain.MakeBlockValue(entry!.RuntimeIndex));
            }

            return text;
        }

        return parameter.Options.Count == 0
            ? text
            : Localization.ClientLanguage.Catalog.Get(
                "TerrainGeneratorOptions",
                parameter.OptionNameKeys[text]
            );
    }
}
