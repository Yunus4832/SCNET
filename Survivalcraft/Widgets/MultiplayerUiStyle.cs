using System.Xml.Linq;

namespace Game.Widgets;

internal static class MultiplayerUiStyle
{
    public static readonly Color ListSelectionColor = new(10, 70, 0, 90);

    public static string Text(string key) => LanguageManager.Get("MultiplayerUI", key);

    public static BevelledButtonWidget CreateButton(string text, Vector2 size)
    {
        return new BevelledButtonWidget
        {
            Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60"),
            Text = text,
            Size = size
        };
    }

    public static BevelledRectangleWidget CreateInsetArea()
    {
        return new BevelledRectangleWidget
        {
            BevelSize = -2f,
            DirectionalLight = 0.15f
        };
    }
}
