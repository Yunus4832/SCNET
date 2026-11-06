using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemPalette : Subsystem
{
    public static IReadOnlyList<Color> DefaultFabricColors { get; } =
        Array.AsReadOnly(CreateFabricColors(WorldPalette.DefaultColors));

    private Color[] _colors = [];

    private Color[] _fabricColors = [];

    private string[] _names = [];

    public Color GetColor(int index) => _colors[index];

    public Color GetFabricColor(int index) => _fabricColors[index];

    public string GetName(int index) => _names[index];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _colors = subsystemGameInfo.WorldSettings.Palette.Colors.ToArray();
        _names = subsystemGameInfo.WorldSettings.Palette.Names.ToArray();
        _fabricColors = CreateFabricColors(_colors);
    }

    private static Color[] CreateFabricColors(IReadOnlyList<Color> colors)
    {
        var result = new Color[WorldPalette.MaxColors];
        for (var i = 0; i < result.Length; i++)
        {
            var rgb = new Vector3(colors[i]);
            var hsv = Color.RgbToHsv(rgb);
            hsv.Y *= 0.85f;
            rgb = Color.HsvToRgb(hsv);
            result[i] = new Color(rgb);
        }

        return result;
    }
}
