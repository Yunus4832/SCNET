using System.Xml.Linq;

using Engine.Media;

namespace Game.Widgets;

public class BitmapButtonWidget : ButtonWidget
{
    public ClickableWidget ClickableWidget { get; }

    private readonly RectangleWidget _imageWidget;

    private readonly LabelWidget _labelWidget;

    public readonly RectangleWidget RectangleWidget;

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

    public required Subtexture NormalSubtexture { get; set; }

    public required Subtexture ClickedSubtexture { get; set; }

    /// <summary>
    ///     文本颜色
    /// </summary>
    public override Color Color { get; set; } = Color.White;

    public void SetImageSize(Vector2 size)
    {
        RectangleWidget.Size = size;
    }

    public void SetFontScale(float scale)
    {
        _labelWidget.FontScale = scale;
    }

    public void SetImageColor(Color color)
    {
        RectangleWidget.FillColor = color;
    }

    public BitmapButtonWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/BitmapButtonContents");
        LoadChildren(this, node);
        RectangleWidget = Children.Find<RectangleWidget>("Button.Rectangle")!;
        _imageWidget = Children.Find<RectangleWidget>("Button.Image")!;
        _labelWidget = Children.Find<LabelWidget>("Button.Label")!;
        ClickableWidget = Children.Find<ClickableWidget>("Button.Clickable")!;
        LoadProperties(this, node);
    }


    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var isEnabledGlobal = IsEnabledGlobal;
        _labelWidget.Color = isEnabledGlobal ? Color : new Color(112, 112, 112);
        _imageWidget.FillColor = isEnabledGlobal ? Color : new Color(112, 112, 112);
        RectangleWidget.Subtexture = ClickableWidget.IsPressed || IsChecked ? ClickedSubtexture : NormalSubtexture;
        base.MeasureOverride(parentAvailableSize);
    }
}
