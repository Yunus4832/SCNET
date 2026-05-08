using System.Xml.Linq;
using Engine.Media;

namespace Game.Widgets;

public class BevelledButtonWidget : ButtonWidget
{
    public ClickableWidget ClickableWidget { get; init; }

    private readonly RectangleWidget _imageWidget;

    private readonly LabelWidget _labelWidget;

    private readonly BevelledRectangleWidget _rectangleWidget;

    public BevelledButtonWidget()
    {
        BevelSize = 2f;
        var node = ContentManager.Get<XElement>("Widgets/BevelledButtonContents");
        LoadChildren(this, node);
        _rectangleWidget = Children.Find<BevelledRectangleWidget>("BevelledButton.Rectangle")!;
        _imageWidget = Children.Find<RectangleWidget>("BevelledButton.Image")!;
        _labelWidget = Children.Find<LabelWidget>("BevelledButton.Label")!;
        ClickableWidget = Children.Find<ClickableWidget>("BevelledButton.Clickable")!;
        _labelWidget.VerticalAlignment = WidgetAlignment.Center;
        LoadProperties(this, node);
    }

    public float FontScale
    {
        get => _labelWidget.FontScale;
        set => _labelWidget.FontScale = value;
    }

    public override bool IsClicked
    {
        get => ClickableWidget.IsClicked;
        set => ClickableWidget.IsClicked = value;
    }

    public override bool IsChecked
    {
        get => ClickableWidget.IsChecked;
        set => ClickableWidget.IsChecked = value;
    }

    public override bool IsAutoCheckingEnabled
    {
        get => ClickableWidget.IsAutoCheckingEnabled;
        set => ClickableWidget.IsAutoCheckingEnabled = value;
    }

    public override string Text
    {
        get => _labelWidget.Text;
        set => _labelWidget.Text = value;
    }

    public override BitmapFont Font
    {
        get => _labelWidget.Font;
        set => _labelWidget.Font = value;
    }

    public Subtexture? Subtexture
    {
        get => _imageWidget.Subtexture;
        set => _imageWidget.Subtexture = value;
    }

    public override Color Color { get; set; } = Color.White;

    public Color BevelColor
    {
        get => _rectangleWidget.BevelColor;
        set => _rectangleWidget.BevelColor = value;
    }

    public Color CenterColor
    {
        get => _rectangleWidget.CenterColor;
        set => _rectangleWidget.CenterColor = value;
    }

    public float AmbientLight
    {
        get => _rectangleWidget.AmbientLight;
        set => _rectangleWidget.AmbientLight = value;
    }

    public float DirectionalLight
    {
        get => _rectangleWidget.DirectionalLight;
        set => _rectangleWidget.DirectionalLight = value;
    }

    public float BevelSize { get; set; }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var isEnabledGlobal = IsEnabledGlobal;
        _labelWidget.Color = isEnabledGlobal ? Color : new Color(112, 112, 112);
        _imageWidget.FillColor = isEnabledGlobal ? Color : new Color(112, 112, 112);
        _rectangleWidget.BevelSize = ClickableWidget.IsPressed || IsChecked ? -0.5f * BevelSize : BevelSize;
        base.MeasureOverride(parentAvailableSize);
    }
}
