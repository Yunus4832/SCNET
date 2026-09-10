using Engine.Serialization;

using EntitySystem.TemplatesDatabase;

namespace Game;

public class WorldPalette
{
    public const int MaxColors = 16;

    public const int MaxNameLength = 16;

    public static readonly Color[] DefaultColors =
    [
        new(255, 255, 255),
        new(181, 255, 255),
        new(255, 181, 255),
        new(160, 181, 255),
        new(255, 240, 160),
        new(181, 255, 181),
        new(255, 181, 160),
        new(181, 181, 181),
        new(112, 112, 112),
        new(32, 112, 112),
        new(112, 32, 112),
        new(26, 52, 128),
        new(87, 54, 31),
        new(24, 116, 24),
        new(136, 32, 32),
        new(24, 24, 24)
    ];

    public Color[] Colors;

    public string[] Names = Enumerable.Repeat(string.Empty, MaxColors).ToArray();

    public WorldPalette()
    {
        Colors = DefaultColors.ToArray();
    }

    public WorldPalette(ValuesDictionary valuesDictionary)
    {
        var array = valuesDictionary.GetValue("Colors", new string(';', 15)).Split(';');
        if (array.Length != 16)
        {
            throw new InvalidOperationException(LanguageManager.Get(GetType().Name, 0));
        }

        Colors = array.Select((s, i) =>
            !string.IsNullOrEmpty(s) ? HumanReadableConverter.ConvertFromString<Color>(s) : DefaultColors[i]).ToArray();
        var array2 = valuesDictionary.GetValue("Names", new string(';', 15)).Split(';');
        if (array2.Length != 16)
        {
            throw new InvalidOperationException(LanguageManager.Get(GetType().Name, 1));
        }

        Names = array2;
        var names = Names;
        var num = 0;
        while (true)
        {
            if (num >= names.Length)
            {
                return;
            }

            if (!string.IsNullOrEmpty(names[num]) && !VerifyColorName(names[num]))
            {
                break;
            }

            num++;
        }

        throw new InvalidOperationException(LanguageManager.Get(GetType().Name, 2));
    }

    public ValuesDictionary Save()
    {
        var valuesDictionary = new ValuesDictionary();
        var value = string.Join(";",
            Colors.Select((c, i) =>
                !(c == DefaultColors[i]) ? HumanReadableConverter.ConvertToString(c) : string.Empty));
        var value2 = string.Join(";", Names);
        valuesDictionary.SetValue("Colors", value);
        valuesDictionary.SetValue("Names", value2);
        return valuesDictionary;
    }

    public void CopyTo(WorldPalette palette)
    {
        palette.Colors = Colors.ToArray();
        palette.Names = Names.ToArray();
    }

    public string GetName(int index)
    {
        return !string.IsNullOrEmpty(Names[index]) ? Names[index] : LanguageManager.GetWorldPalette(index);
    }

    public static bool VerifyColorName(string name)
    {
        return name.Length is >= 1 and <= 16 && name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ');
    }
}
