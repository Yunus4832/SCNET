using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemPalette : Subsystem
{
    public static readonly Color[] DefaultFabricColors;

    private Color[] _colors = [];

    private Color[] _fabricColors = [];

    private string[] _names = [];

    static SubsystemPalette()
    {
        DefaultFabricColors = new Color[16];
        DefaultFabricColors = CreateFabricColors(WorldPalette.DefaultColors);
    }

    private Color GetColor(int index)
    {
        return _colors[index];
    }

    public string GetName(int index)
    {
        return _names[index];
    }

    private Color GetFabricColor(int index)
    {
        return _fabricColors[index];
    }

    public static Color GetColor(BlockGeometryGenerator generator, int? index)
    {
        return index.HasValue ? generator.SubsystemPalette.GetColor(index.Value) : Color.White;
    }

    public static Color GetColor(DrawBlockEnvironmentData environmentData, int? index)
    {
        return GetColor(environmentData.SubsystemTerrain, index);
    }

    public static Color GetColor(SubsystemTerrain? subsystemTerrain, int? index)
    {
        if (index.HasValue)
        {
            return subsystemTerrain is { SubsystemPalette: not null }
                ? subsystemTerrain.SubsystemPalette.GetColor(index.Value)
                : WorldPalette.DefaultColors[index.Value];
        }

        return Color.White;
    }

    public static Color GetFabricColor(BlockGeometryGenerator generator, int? index)
    {
        return index.HasValue ? generator.SubsystemPalette.GetFabricColor(index.Value) : Color.White;
    }

    public static Color GetFabricColor(DrawBlockEnvironmentData environmentData, int? index)
    {
        return GetFabricColor(environmentData.SubsystemTerrain, index);
    }

    public static Color GetFabricColor(SubsystemTerrain? subsystemTerrain, int? index)
    {
        if (index.HasValue)
        {
            return subsystemTerrain is { SubsystemPalette: not null }
                ? subsystemTerrain.SubsystemPalette.GetFabricColor(index.Value)
                : DefaultFabricColors[index.Value];
        }

        return Color.White;
    }

    public static string GetName(int? index, string suffix = "")
    {
        if (index is null)
        {
            return suffix;
        }

        var text = LanguageManager.GetWorldPalette(index.Value);
        if (!string.IsNullOrEmpty(suffix))
        {
            return text + " " + suffix;
        }

        return text;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _colors = subsystemGameInfo.WorldSettings.Palette.Colors.ToArray();
        _names = subsystemGameInfo.WorldSettings.Palette.Names.ToArray();
        _fabricColors = CreateFabricColors(_colors);
    }

    private static Color[] CreateFabricColors(Color[] colors)
    {
        var array = new Color[16];
        for (var i = 0; i < 16; i++)
        {
            var rgb = new Vector3(colors[i]);
            var hsv = Color.RgbToHsv(rgb);
            hsv.Y *= 0.85f;
            rgb = Color.HsvToRgb(hsv);
            array[i] = new Color(rgb);
        }

        return array;
    }
}
