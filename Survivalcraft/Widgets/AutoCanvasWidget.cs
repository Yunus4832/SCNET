using System.Text;

using Engine.Graphics;
using Engine.Serialization;

using Game.Network;

namespace Game.Widgets;

public class AutoCanvasWidget : CanvasWidget
{
    private static readonly Dictionary<string, Color> _colorMap = new();

    private readonly StringBuilder _caches = new();

    private readonly Dictionary<ClickableWidget, Action> _events = new();

    private List<ParsedItem> _renderItems = [];

    static AutoCanvasWidget()
    {
        var type = typeof(Color);
        foreach (var fieldInfo in type.GetFields())
        {
            if (fieldInfo.IsStatic)
            {
                _colorMap.Add(fieldInfo.Name.ToLower(), (Color)fieldInfo.GetValue(null)!);
            }
        }
    }

    public string ContentText
    {
        get;
        set
        {
            _renderItems.Clear();
            _caches.Clear();
            field = value;
            _renderItems = SimpleTagParser.Parse(value);
            ApplyRenderItems();
        }
    } = string.Empty;

    public void Add(Widget widget)
    {
        Children.Add(widget);
    }

    public void Remove(Widget widget)
    {
        Children.Remove(widget);
    }

    public void ApplyRenderItems()
    {
        Children.Clear();
        foreach (var item in _renderItems)
        {
            if (!item.IsTag)
            {
                var labelWidget = new LabelWidget
                {
                    Text = item.Content,
                    Margin = new Vector2(0, -5),
                    FontScale = 1f,
                    WordWrap = true
                };
                AddChildren(labelWidget);
                continue;
            }

            switch (item.TagName)
            {
                case "c":
                    var labelWidget2 = new LabelWidget
                    {
                        Text = item.Content,
                        Margin = new Vector2(0, -5), Color = Color.White, FontScale = 1f,
                        WordWrap = true
                    };
                    if (_colorMap.TryGetValue(item.Value.ToLower(), out var color))
                    {
                        labelWidget2.Color = color;
                    }
                    else
                    {
                        HumanReadableConverter.TryConvertFromString(
                            typeof(Color),
                            item.Content,
                            out var result
                        );
                        if (result != null)
                        {
                            var color1 = (Color)result;
                            labelWidget2.Color = color1;
                        }
                    }

                    AddChildren(labelWidget2);
                    break;
                case "em":
                    // 如果解析失败，跳过
                    if(!int.TryParse(item.Content, out var id))
                    {
                        break;
                    }

                    id = MathUtils.Clamp(id, 10, 90);
                    var rectangle = new RectangleWidget();
                    rectangle.Size = new Vector2(24f);
                    rectangle.VerticalAlignment = WidgetAlignment.Far;
                    rectangle.HorizontalAlignment = WidgetAlignment.Center;
                    rectangle.FillColor = Color.White;
                    rectangle.OutlineColor = Color.Transparent;
                    rectangle.Subtexture = new Subtexture(ContentManager.Get<Texture2D>("Textures/emojis/" + id),
                        Vector2.Zero, Vector2.One);
                    AddChildren(rectangle);
                    break;
                case "b":
                    // 如果解析失败，跳过
                    if(!int.TryParse(item.Content, out var id2))
                    {
                        break;
                    }

                    //修正会被方块遮挡问题
                    var block = new BlockIconWidget { VerticalAlignment = WidgetAlignment.Center, Depth = 0 };
                    block.Size = new Vector2(24f);
                    block.Value = Terrain.ExtractContents(id2);
                    AddChildren(block);
                    break;
                case "p":
                    labelWidget2 = new LabelWidget
                    {
                        Text = "*位置*",
                        Margin = new Vector2(0, -5),
                        Color = Color.SkyBlue,
                        FontScale = 1f,
                        WordWrap = true
                    };
                    var clickWidget = new ClickableWidget();
                    var c = new CanvasWidget { Size = new Vector2(82, 32) };
                    c.AddChildren(clickWidget);
                    c.AddChildren(labelWidget2);
                    _events.Add(clickWidget, () =>
                    {
                        var pp = HumanReadableConverter.ConvertFromString<Vector3>(item.Content);
                        if (CommonLib.MainPlayer != null)
                        {
                            CommonLib.MainPlayer.ComponentBody.Position = pp;
                        }
                    });
                    AddChildren(c);
                    break;
                default:
                    labelWidget2 = new LabelWidget
                    {
                        Text = item.Content,
                        Margin = new Vector2(0, -5),
                        Color = Color.White,
                        FontScale = 1f,
                        WordWrap = true
                    };
                    AddChildren(labelWidget2);
                    break;
            }
        }
    }

    public override void Update()
    {
        foreach (var item in _events)
        {
            if (item.Key.IsClicked)
            {
                item.Value?.Invoke();
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var currentX = 0f;
        var currentY = 0f;
        var currentLineHeight = 0f;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Measure(new Vector2(parentAvailableSize.X, 0f));
                currentLineHeight = MathUtils.Max(currentLineHeight, child.DesiredSize.Y);
                if (currentX + child.DesiredSize.X > parentAvailableSize.X)
                {
                    currentY += currentLineHeight;
                    currentX = 0f;
                    currentLineHeight = MathUtils.Max(0f, child.DesiredSize.Y);
                }

                currentX += child.DesiredSize.X;
            }
        }

        DesiredSize = new Vector2(currentX, currentY + currentLineHeight);
        Size = DesiredSize;
    }

    public override void ArrangeOverride()
    {
        var startX = 0f;
        var startY = 0f;
        var currentLineHeight = 0f;

        foreach (var widget in Children)
        {
            if (!widget.IsVisible)
            {
                continue;
            }

            widget.Arrange(new Vector2(startX, startY), new Vector2(widget.DesiredSize.X, widget.DesiredSize.Y));
            currentLineHeight = MathUtils.Max(currentLineHeight, widget.ActualSize.Y);
            if (widget.ActualSize.X + startX > ActualSize.X)
            {
                //屏幕宽度不足，换到下一行
                startY += currentLineHeight;
                startX = 0f;
                currentLineHeight = MathUtils.Max(0f, widget.ActualSize.Y);
                widget.Arrange(new Vector2(startX, startY) + widget.Margin, widget.ActualSize);
            }

            startX += widget.ActualSize.X;
        }
    }

}
