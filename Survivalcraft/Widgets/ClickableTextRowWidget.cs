namespace Game.Widgets;

public class ClickableTextRowWidget : CanvasWidget
{
    private readonly ClickableWidget _clickableWidget = new()
    {
        SoundName = "Audio/UI/ButtonClick"
    };

    private readonly LabelWidget _labelWidget = new()
    {
        FontScale = 0.8f,
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center,
        Color = Color.White
    };

    public bool IsClicked => _clickableWidget.IsClicked;

    public string Text
    {
        get => _labelWidget.Text;
        set => _labelWidget.Text = value;
    }

    public float FontScale
    {
        get => _labelWidget.FontScale;
        set => _labelWidget.FontScale = value;
    }

    public Color Color
    {
        get => _labelWidget.Color;
        set => _labelWidget.Color = value;
    }

    public string? SoundName
    {
        get => _clickableWidget.SoundName;
        set => _clickableWidget.SoundName = value;
    }

    public ClickableTextRowWidget()
    {
        Size = new Vector2(float.PositiveInfinity, 58);
        Children.Add(_labelWidget);
        Children.Add(_clickableWidget);
    }

    public ClickableTextRowWidget(string text) : this()
    {
        Text = text;
    }
}
