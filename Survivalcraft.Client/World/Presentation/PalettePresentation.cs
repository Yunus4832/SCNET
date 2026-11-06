namespace Game.World.Presentation;

public static class PalettePresentation
{
    public static Color GetColor(SubsystemPalette palette, int? index)
    {
        return index.HasValue ? palette.GetColor(index.Value) : Color.White;
    }

    public static Color GetColor(SubsystemTerrain? terrain, int? index)
    {
        if (index is not { } color) { return Color.White; }

        return terrain is { SubsystemPalette: not null }
            ? terrain.SubsystemPalette.GetColor(color)
            : WorldPalette.DefaultColors[color];
    }

    public static Color GetFabricColor(SubsystemPalette palette, int? index)
    {
        return index.HasValue ? palette.GetFabricColor(index.Value) : Color.White;
    }

    public static Color GetFabricColor(SubsystemTerrain? terrain, int? index)
    {
        if (index is not { } color) { return Color.White; }

        return terrain is { SubsystemPalette: not null }
            ? terrain.SubsystemPalette.GetFabricColor(color)
            : SubsystemPalette.DefaultFabricColors[color];
    }

    public static string GetName(int? index, string suffix = "")
    {
        return index is { } color
            ? ComposeName(Localization.ClientLanguage.Catalog.GetWorldPalette(color), suffix)
            : suffix;
    }

    public static string GetName(SubsystemPalette palette, int index, string suffix = "")
    {
        var customName = palette.GetName(index);
        return ComposeName(
            string.IsNullOrEmpty(customName)
                ? Localization.ClientLanguage.Catalog.GetWorldPalette(index)
                : customName,
            suffix
        );
    }

    private static string ComposeName(string name, string suffix)
    {
        return string.IsNullOrEmpty(suffix) ? name : name + " " + suffix;
    }
}
