using Engine.Graphics;

namespace Game.Widgets;

public sealed class ExpandableButtonWidget : CanvasWidget
{
    private readonly BevelledButtonWidget _button = new() { Size = new Vector2(60f) };

    private readonly RectangleWidget _icon = new()
    {
        Size = new Vector2(28f), HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center, FillColor = Color.White,
        OutlineColor = Color.Transparent, IsHitTestVisible = false
    };

    private readonly CanvasWidget _popup = new();

    private readonly StackPanelWidget _items = new() { Direction = LayoutDirection.Vertical, Margin = new Vector2(4f) };

    private readonly List<(ClickableTextRowWidget Button, ExpandableButtonItem Item)> _bindings = [];

    private Screen? _popupHost;

    private string _iconName = string.Empty;

    public ExpandableButtonWidget()
    {
        Size = new Vector2(60f);
        _button.Children.Add(_icon);
        Children.Add(_button);
        _popup.Children.Add(
            new BevelledRectangleWidget
            {
                DirectionalLight = 0f, AmbientLight = 1f, BevelColor = Color.Transparent,
                CenterColor = new Color(0, 0, 0, 128), ShadowColor = Color.Transparent,
                BevelSize = 0f, RoundingRadius = 8f, RoundingCount = 4
            }
        );
        _popup.Children.Add(_items);
        _popup.IsVisible = false;
        Children.Add(_popup);
    }

    public string Icon
    {
        get => _iconName;
        set
        {
            _iconName = value;
            _icon.Subtexture = ContentManager.Get<Subtexture>(value);
        }
    }

    public Func<IReadOnlyList<ExpandableButtonItem>> Items { get; set; } = () => [];

    public Vector2 ItemSize { get; set; } = new(180f, 44f);

    public WidgetAlignment MenuAlignment { get; set; } = WidgetAlignment.Near;

    public bool AvoidScreenEdges { get; set; } = true;

    public bool IsOpen => _popupHost != null;

    public void Open()
    {
        Close();
        var host = ParentWidget;
        while (host != null && host is not Screen)
        {
            host = host.ParentWidget;
        }

        if (host is not Screen screen)
        {
            return;
        }

        foreach (var other in screen.AllChildren.OfType<ExpandableButtonWidget>().ToArray())
        {
            other.Close();
        }

        _items.Children.Clear();
        _bindings.Clear();
        foreach (var item in Items())
        {
            var row = new ClickableTextRowWidget(item.Text) { Size = ItemSize, FontScale = 0.8f };
            _items.Children.Add(row);
            _bindings.Add((row, item));
        }

        if (_bindings.Count == 0)
        {
            return;
        }

        _popup.Size = new Vector2(ItemSize.X + 8f, ItemSize.Y * _bindings.Count + 8f);
        _popupHost = screen;
        _popup.IsVisible = true;
        _button.IsChecked = true;
    }

    public void Close()
    {
        _popup.IsVisible = false;
        _popupHost = null;
        _button.IsChecked = false;
    }

    public override void ArrangeOverride()
    {
        base.ArrangeOverride();
        if (_popupHost is not { } host)
        {
            return;
        }

        var origin = Vector2.Transform(Vector2.Transform(Vector2.Zero, GlobalTransform), host.InvertedGlobalTransform);
        var end = Vector2.Transform(Vector2.Transform(ActualSize, GlobalTransform), host.InvertedGlobalTransform);
        var y = MenuAlignment switch
        {
            WidgetAlignment.Far => end.Y - _popup.Size.Y,
            WidgetAlignment.Center => (origin.Y + end.Y - _popup.Size.Y) / 2f,
            _ => origin.Y + 6f
        };
        var x = end.X + 4f;
        if (AvoidScreenEdges)
        {
            if (x + _popup.Size.X > host.ActualSize.X - 8f)
            {
                x = origin.X - _popup.Size.X - 4f;
            }

            x = MathUtils.Clamp(x, 8f, MathUtils.Max(8f, host.ActualSize.X - _popup.Size.X - 8f));
            y = MathUtils.Clamp(y, 8f, MathUtils.Max(8f, host.ActualSize.Y - _popup.Size.Y - 8f));
        }

        var position = Vector2.Transform(
            Vector2.Transform(new Vector2(x, y), host.GlobalTransform),
            InvertedGlobalTransform
        );
        _popup.Measure(_popup.Size);
        _popup.Arrange(position, _popup.Size);
    }

    public override void UpdateCeases()
    {
        Close();
        base.UpdateCeases();
    }

    public override void Update()
    {
        if (_button.IsClicked)
        {
            if (IsOpen) { Close(); }
            else { Open(); }

            return;
        }

        if (!IsVisibleGlobal || !IsEnabledGlobal)
        {
            Close();
        }

        foreach (var (button, item) in _bindings)
        {
            if (IsOpen && button.IsClicked)
            {
                Input.Clear();
                Close();
                item.Selected();
                return;
            }
        }

        if (IsOpen &&
            Input.Click.HasValue &&
            !_popup.HitTest(Input.Click.Value.End) &&
            !_button.HitTest(Input.Click.Value.End))
        {
            Close();
        }
    }
}
