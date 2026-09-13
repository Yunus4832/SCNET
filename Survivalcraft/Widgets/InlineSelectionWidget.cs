using System.Xml.Linq;

namespace Game.Widgets;

public sealed class InlineSelectionWidget : CanvasWidget
{
    private const float _listPadding = 4f;
    private const float _separatorThickness = 2f;

    private readonly BevelledRectangleWidget _background;
    private readonly CanvasWidget _header;
    private readonly ClickableWidget _headerClickable;
    private readonly LabelWidget _headerLabel;
    private readonly List<object> _items = [];
    private readonly ListPanelWidget _list;
    private readonly CanvasWidget _listViewport;
    private readonly RectangleWidget _separator;
    private readonly CanvasWidget _surface;

    private Func<object, string> _itemTextProvider = item => item.ToString() ?? string.Empty;

    public InlineSelectionWidget()
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/InlineSelectionWidget"));
        _surface = Children.Find<CanvasWidget>("InlineSelection.Surface")!;
        _background = Children.Find<BevelledRectangleWidget>("InlineSelection.Background")!;
        _header = Children.Find<CanvasWidget>("InlineSelection.Header")!;
        _headerLabel = Children.Find<LabelWidget>("InlineSelection.HeaderLabel")!;
        _headerClickable = Children.Find<ClickableWidget>("InlineSelection.HeaderClickable")!;
        _listViewport = Children.Find<CanvasWidget>("InlineSelection.ListViewport")!;
        _list = Children.Find<ListPanelWidget>("InlineSelection.List")!;
        _separator = Children.Find<RectangleWidget>("InlineSelection.Separator")!;
        _list.ItemWidgetFactory = CreateItemWidget;
        _list.ScrollPosition = 0f;
        _list.ScrollSpeed = 0f;
        _list.ItemClicked += _ =>
        {
            Input.Clear();
            Close();
        };
        _list.SelectionChanged += () =>
        {
            UpdateHeaderText();
            SelectionChanged?.Invoke();
        };
        UpdateVisualState();
    }

    public event Action? SelectionChanged;

    public Vector2 CollapsedSize
    {
        get;
        set
        {
            if (value.X < 0f || value.Y < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
            UpdateVisualState();
        }
    } = new(310f, 60f);

    public int MaxVisibleItems
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            UpdateVisualState();
        }
    } = 5;

    public float ItemHeight
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            _list.ItemSize = value;
            UpdateVisualState();
        }
    } = 52f;

    public float SurfaceMargin
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
            UpdateVisualState();
        }
    } = 6f;

    public WidgetAlignment HeaderHorizontalAlignment
    {
        get => _headerLabel.HorizontalAlignment;
        set => _headerLabel.HorizontalAlignment = value;
    }

    public Vector2 HeaderContentMargin
    {
        get => _headerLabel.Margin;
        set => _headerLabel.Margin = value;
    }

    public float FontScale
    {
        get => _headerLabel.FontScale;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _headerLabel.FontScale = value;
        }
    }

    public float ItemFontScale
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            RebuildList();
        }
    } = 0.8f;

    public string PlaceholderText
    {
        get;
        set
        {
            field = value ?? string.Empty;
            UpdateHeaderText();
        }
    } = string.Empty;

    public Func<object, string> ItemTextProvider
    {
        get => _itemTextProvider;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _itemTextProvider = value;
            RebuildList();
            UpdateHeaderText();
        }
    }

    public ReadOnlyList<object> Items => new(_items);

    public int? SelectedIndex
    {
        get => _list.SelectedIndex;
        set => _list.SelectedIndex = value;
    }

    public object? SelectedItem
    {
        get => _list.SelectedItem;
        set => _list.SelectedItem = value;
    }

    public bool IsOpen { get; private set; }

    public Color Color
    {
        get => _headerLabel.Color;
        set
        {
            _headerLabel.Color = value;
            RebuildList();
        }
    }

    public Color CenterColor
    {
        get => _background.CenterColor;
        set => _background.CenterColor = value;
    }

    public Color BevelColor
    {
        get => _background.BevelColor;
        set => _background.BevelColor = value;
    }

    public Color SelectionColor
    {
        get => _list.SelectionColor;
        set => _list.SelectionColor = value;
    }

    public void SetItems(IEnumerable<object> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items.Clear();
        _items.AddRange(items);
        RebuildList();
        UpdateHeaderText();
        if (_items.Count == 0)
        {
            IsOpen = false;
        }

        UpdateVisualState();
    }

    public void AddItem(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        _list.AddItem(item);
        UpdateVisualState();
    }

    public void ClearItems()
    {
        _items.Clear();
        _list.ClearItems();
        IsOpen = false;
        UpdateHeaderText();
        UpdateVisualState();
    }

    public void RefreshItems()
    {
        RebuildList();
        UpdateHeaderText();
    }

    public void Open()
    {
        if (_items.Count == 0)
        {
            return;
        }

        IsOpen = true;
        _list.ScrollPosition = 0f;
        UpdateVisualState();
    }

    public void Close()
    {
        IsOpen = false;
        UpdateVisualState();
    }

    public override void Update()
    {
        if (IsOpen && Input.Back)
        {
            Input.Clear();
            Close();
            return;
        }

        if (_headerClickable.IsClicked)
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        UpdateVisualState();
        base.MeasureOverride(parentAvailableSize);
    }

    public override void ArrangeOverride()
    {
        SetWidgetPosition(_surface, new Vector2(SurfaceMargin));
        var headerSize = CalculateHeaderSize();
        _surface.SetWidgetPosition(_header, Vector2.Zero);
        _surface.SetWidgetPosition(_listViewport, new Vector2(_listPadding, headerSize.Y + _listPadding));
        _surface.SetWidgetPosition(_separator, new Vector2(0f, headerSize.Y));
        base.ArrangeOverride();
    }

    private Widget CreateItemWidget(object item)
    {
        return new LabelWidget
        {
            Text = _itemTextProvider(item),
            FontScale = ItemFontScale,
            Color = Color,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
    }

    private void RebuildList()
    {
        var selectedItem = SelectedItem;
        _list.ClearItems();
        foreach (var item in _items)
        {
            _list.AddItem(item);
        }

        if (selectedItem != null)
        {
            _list.SelectedItem = selectedItem;
        }
    }

    private void UpdateHeaderText()
    {
        _headerLabel.Text = SelectedItem is { } selectedItem
            ? _itemTextProvider(selectedItem)
            : PlaceholderText;
    }

    private void UpdateVisualState()
    {
        var listHeight = CalculateListHeight();
        Size = new Vector2(CollapsedSize.X, CollapsedSize.Y + (IsOpen ? listHeight : 0f));
        var headerSize = CalculateHeaderSize();
        var surfaceSize = new Vector2(headerSize.X, headerSize.Y + (IsOpen ? listHeight : 0f));
        _surface.Size = surfaceSize;
        _background.Size = surfaceSize;
        _background.BevelSize = _headerClickable.IsPressed ? -1f : 2f;
        _header.Size = headerSize;
        _listViewport.Size = new Vector2(
            Math.Max(headerSize.X - 2f * _listPadding, 0f),
            Math.Max(listHeight - 2f * _listPadding, 0f));
        _listViewport.IsVisible = IsOpen;
        _separator.Size = new Vector2(headerSize.X, _separatorThickness);
        _separator.IsVisible = IsOpen;
    }

    private Vector2 CalculateHeaderSize()
    {
        return Vector2.Max(CollapsedSize - new Vector2(2f * SurfaceMargin), Vector2.Zero);
    }

    private float CalculateListHeight()
    {
        return _items.Count > 0
            ? Math.Min(_items.Count, MaxVisibleItems) * ItemHeight + 2f * _listPadding
            : 0f;
    }
}
