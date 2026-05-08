using System.Xml.Linq;
using Engine.Graphics;
using Engine.Media;

namespace Game.Widgets;

public class LinkWidget : FixedSizePanelWidget
{
    private readonly ClickableWidget _clickableWidget;

    private readonly LabelWidget _labelWidget;

    public Vector2 Size
    {
        get => _labelWidget.Size;
        set => _labelWidget.Size = value;
    }

    public bool IsClicked => _clickableWidget.IsClicked;

    public bool IsPressed => _clickableWidget.IsPressed;

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

    public TextAnchor TextAnchor
    {
        get => _labelWidget.TextAnchor;
        set => _labelWidget.TextAnchor = value;
    }

    public BitmapFont Font
    {
        get => _labelWidget.Font;
        set => _labelWidget.Font = value;
    }

    public Color Color
    {
        get => _labelWidget.Color;
        set => _labelWidget.Color = value;
    }

    public bool DropShadow
    {
        get => _labelWidget.DropShadow;
        set => _labelWidget.DropShadow = value;
    }

    private string Url { get; set; } = string.Empty;

    public LinkWidget()
    {
        var node = ContentManager.Get<XElement>("Widgets/LinkContents");
        LoadChildren(this, node);
        _labelWidget = Children.Find<LabelWidget>("Label")!;
        _clickableWidget = Children.Find<ClickableWidget>("Clickable")!;
        LoadProperties(this, node);
    }


    public override void Update()
    {
        if (!string.IsNullOrEmpty(Url) && IsClicked)
        {
            WebBrowserManager.LaunchBrowser(Url);
        }
    }
}
