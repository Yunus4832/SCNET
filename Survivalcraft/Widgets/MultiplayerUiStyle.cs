using System.Xml.Linq;

namespace Game.Widgets;

internal static class MultiplayerUiStyle
{
    public static readonly Color ListSelectionColor = new(10, 70, 0, 90);

    public static readonly Color SecondaryTextColor =
        new(225, 225, 225, 210);

    public static string Text(string key) => LanguageManager.Get("MultiplayerUI", key);

    public static BevelledButtonWidget CreateButton(string text, Vector2 size)
    {
        var button = new BevelledButtonWidget
        {
            Style = ContentManager.Get<XElement>("Styles/ButtonStyle_160x60"),
            Size = size
        };
        SetButtonText(button, text);
        return button;
    }

    public static void SetButtonText(
        BevelledButtonWidget button,
        string text,
        float minimumScale = 0.62f)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.Text = text;
        button.FontScale = 1f;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var measuredWidth = button.Font.MeasureText(
            text,
            Vector2.One,
            Vector2.Zero).X;
        var availableWidth = MathUtils.Max(button.Size.X - 18f, 1f);
        if (measuredWidth > availableWidth)
        {
            button.FontScale = MathUtils.Max(
                minimumScale,
                availableWidth / measuredWidth);
        }
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
