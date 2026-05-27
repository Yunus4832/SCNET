using System.Xml.Linq;

using Engine.Media;

namespace Game.Widgets;

public class CheckboxWidget : CanvasWidget
{
    private readonly CanvasWidget _canvasWidget;

    private readonly ClickableWidget _clickableWidget;

    private readonly LabelWidget _labelWidget;

    private readonly RectangleWidget _rectangleWidget;

    private readonly RectangleWidget _tickWidget;

    public bool IsPressed => _clickableWidget.IsPressed;

    public bool IsClicked => _clickableWidget.IsClicked;

    public bool IsTapped => _clickableWidget.IsTapped;

    public bool IsChecked { get; set; }

    public bool IsAutoCheckingEnabled { get; set; }

    public string Text
    {
        get => _labelWidget.Text;
        set => _labelWidget.Text = value;
    }

    public BitmapFont Font
    {
        get => _labelWidget.Font;
        set => _labelWidget.Font = value;
    }

    public Subtexture? TickSubtexture
    {
        get => _tickWidget.Subtexture;
        set => _tickWidget.Subtexture = value;
    }

    public Color Color { get; set; }

    public Vector2 CheckboxSize
    {
        get => _canvasWidget.Size;
        set => _canvasWidget.Size = value;
    }

    public event Action<bool>? CheckStatusChanged;

    public CheckboxWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/CheckboxContents");
        LoadChildren(this, node);
        _canvasWidget = Children.Find<CanvasWidget>("Checkbox.Canvas")!;
        _rectangleWidget = Children.Find<RectangleWidget>("Checkbox.Rectangle")!;
        _tickWidget = Children.Find<RectangleWidget>("Checkbox.Tick")!;
        _labelWidget = Children.Find<LabelWidget>("Checkbox.Label")!;
        _clickableWidget = Children.Find<ClickableWidget>("Checkbox.Clickable")!;
        LoadProperties(this, node);
    }


    public override void Update()
    {
        if (IsClicked && IsAutoCheckingEnabled)
        {
            IsChecked = !IsChecked;
            CheckStatusChanged?.Invoke(IsChecked);
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var isEnabledGlobal = IsEnabledGlobal;
        _labelWidget.Color = isEnabledGlobal ? Color : new Color(112, 112, 112);
        _rectangleWidget.FillColor = new Color(0, 0, 0, 128);
        _rectangleWidget.OutlineColor = isEnabledGlobal ? new Color(128, 128, 128) : new Color(112, 112, 112);
        _tickWidget.IsVisible = IsChecked;
        _tickWidget.FillColor = isEnabledGlobal ? Color : new Color(112, 112, 112);
        _tickWidget.OutlineColor = Color.Transparent;
        _tickWidget.Subtexture = TickSubtexture;
        base.MeasureOverride(parentAvailableSize);
    }
}
