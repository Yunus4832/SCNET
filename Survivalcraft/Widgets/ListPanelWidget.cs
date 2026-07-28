using Engine.Graphics;

namespace Game.Widgets;

public class ListPanelWidget : ScrollPanelWidget
{
    private Vector2 _lastActualSize = new(-1f);

    private bool _clickAllowed;

    private int _firstVisibleIndex;

    private readonly List<object> _items = [];

    private int _lastVisibleIndex;

    private int _selectedItemIndex = -1;

    public readonly Dictionary<int, Widget> WidgetsByIndex = new();

    private bool _widgetsDirty;

    public bool PlayClickSound = true;

    public bool IsSelectionEnabled
    {
        get;
        set
        {
            field = value;
            if (!value)
            {
                SelectedIndex = null;
            }
        }
    } = true;

    public Func<object, Widget> ItemWidgetFactory { get; set; }

    public override LayoutDirection Direction
    {
        get => base.Direction;
        set
        {
            if (value == Direction)
            {
                return;
            }

            base.Direction = value;
            _widgetsDirty = true;
        }
    }

    public override float ScrollPosition
    {
        get => base.ScrollPosition;
        set
        {
            if (value.CloseTo(ScrollPosition))
            {
                return;
            }

            base.ScrollPosition = value;
            _widgetsDirty = true;
        }
    }

    public float ItemSize
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            _widgetsDirty = true;
        }
    }

    public int? SelectedIndex
    {
        get => _selectedItemIndex == -1 ? null : _selectedItemIndex;
        set
        {
            if (value.HasValue && (value.Value < 0 || value.Value >= _items.Count))
            {
                value = null;
            }

            if (value == _selectedItemIndex)
            {
                return;
            }

            _selectedItemIndex = value ?? -1;
            SelectionChanged?.Invoke();
        }
    }

    public object? SelectedItem
    {
        get => _selectedItemIndex == -1 ? null : _items[_selectedItemIndex];
        set
        {
            if (value is null)
            {
                SelectedIndex = null;
                return;
            }
            var num = _items.IndexOf(value);
            SelectedIndex = num >= 0 ? new int?(num) : null;
        }
    }

    public ReadOnlyList<object> Items => new(_items);

    public Color SelectionColor { get; set; }

    public virtual event Action<object>? ItemClicked;

    public virtual event Action? SelectionChanged;

    public ListPanelWidget()
    {
        SelectionColor = Color.Gray;
        ItemWidgetFactory = item => new LabelWidget
        {
            Text = item.ToString() ?? string.Empty,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        ItemSize = 48f;
    }


    public void AddItem(object item)
    {
        _items.Add(item);
        _widgetsDirty = true;
    }

    public void RemoveItem(object item)
    {
        var num = _items.IndexOf(item);
        if (num >= 0)
        {
            RemoveItemAt(num);
        }
    }

    public void RemoveItemAt(int index)
    {
        _ = _items[index];
        _items.RemoveAt(index);
        WidgetsByIndex.Clear();
        _widgetsDirty = true;
        if (index == SelectedIndex)
        {
            SelectedIndex = null;
        }
    }

    public void ClearItems()
    {
        _items.Clear();
        WidgetsByIndex.Clear();
        _widgetsDirty = true;
        SelectedIndex = null;
    }

    public override float CalculateScrollAreaLength()
    {
        return Items.Count * ItemSize;
    }

    public void ScrollToItem(object item)
    {
        var num = _items.IndexOf(item);
        if (num >= 0)
        {
            var num2 = num * ItemSize;
            var num3 = Direction == LayoutDirection.Horizontal ? ActualSize.X : ActualSize.Y;
            if (num2 < ScrollPosition)
            {
                ScrollPosition = num2;
            }
            else if (num2 > ScrollPosition + num3 - ItemSize)
            {
                ScrollPosition = num2 - num3 + ItemSize;
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                if (Direction == LayoutDirection.Horizontal)
                {
                    child.Measure(new Vector2(ItemSize,
                        MathUtils.Max(parentAvailableSize.Y - 2f * child.Margin.Y, 0f)));
                }
                else
                {
                    child.Measure(new Vector2(MathUtils.Max(parentAvailableSize.X - 2f * child.Margin.X, 0f),
                        ItemSize));
                }
            }
        }

        if (!_widgetsDirty)
        {
            return;
        }

        _widgetsDirty = false;
        CreateListWidgets(Direction == LayoutDirection.Horizontal ? ActualSize.X : ActualSize.Y);
    }

    public override void ArrangeOverride()
    {
        if (ActualSize != _lastActualSize)
        {
            _widgetsDirty = true;
        }

        _lastActualSize = ActualSize;
        var num = _firstVisibleIndex;
        foreach (var child in Children)
        {
            if (Direction == LayoutDirection.Horizontal)
            {
                var vector = new Vector2(num * ItemSize - ScrollPosition, 0f);
                ArrangeChildWidgetInCell(vector, vector + new Vector2(ItemSize, ActualSize.Y), child);
            }
            else
            {
                var vector2 = new Vector2(0f, num * ItemSize - ScrollPosition);
                ArrangeChildWidgetInCell(vector2, vector2 + new Vector2(ActualSize.X, ItemSize), child);
            }

            num++;
        }
    }

    public override void Update()
    {
        var flag = ScrollSpeed != 0f;
        base.Update();
        if (!IsSelectionEnabled)
        {
            _clickAllowed = false;
            return;
        }

        if (Input.Tap.HasValue && HitTestPanel(Input.Tap.Value))
        {
            _clickAllowed = !flag;
        }

        if (!Input.Click.HasValue || !_clickAllowed || !HitTestPanel(Input.Click.Value.Start) ||
            !HitTestPanel(Input.Click.Value.End))
        {
            return;
        }

        var num = PositionToItemIndex(Input.Click.Value.End);
        if (ItemClicked != null && num >= 0 && num < _items.Count)
        {
            ItemClicked?.Invoke(Items[num]);
        }

        SelectedIndex = num;
        if (SelectedIndex.HasValue && PlayClickSound)
        {
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }
    }

    public override void Draw(DrawContext dc)
    {
        if (IsSelectionEnabled &&
            SelectedIndex.HasValue && SelectedIndex.Value >= _firstVisibleIndex &&
            SelectedIndex.Value <= _lastVisibleIndex)
        {
            var vector = Direction == LayoutDirection.Horizontal
                ? new Vector2(SelectedIndex.Value * ItemSize - ScrollPosition, 0f)
                : new Vector2(0f, SelectedIndex.Value * ItemSize - ScrollPosition);
            var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
            var count = flatBatch2D.TriangleVertices.Count;
            var v = Direction == LayoutDirection.Horizontal
                ? new Vector2(ItemSize, ActualSize.Y)
                : new Vector2(ActualSize.X, ItemSize);
            flatBatch2D.QueueQuad(vector, vector + v, 0f, SelectionColor * GlobalColorTransform);
            flatBatch2D.TransformTriangles(GlobalTransform, count);
        }

        base.Draw(dc);
    }

    public int PositionToItemIndex(Vector2 position)
    {
        var vector = ScreenToWidget(position);
        if (Direction == LayoutDirection.Horizontal)
        {
            return (int)((vector.X + ScrollPosition) / ItemSize);
        }

        return (int)((vector.Y + ScrollPosition) / ItemSize);
    }

    public void CreateListWidgets(float size)
    {
        Children.Clear();
        if (_items.Count <= 0)
        {
            return;
        }

        var x = (int)MathUtils.Floor(ScrollPosition / ItemSize);
        var x2 = (int)MathUtils.Floor((ScrollPosition + size) / ItemSize);
        _firstVisibleIndex = MathUtils.Max(x, 0);
        _lastVisibleIndex = MathUtils.Min(x2, _items.Count - 1);
        for (var i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var obj = _items[i];
            if (!WidgetsByIndex.TryGetValue(i, out var value))
            {
                value = ItemWidgetFactory(obj);
                value.Tag = obj;
                WidgetsByIndex.Add(i, value);
            }

            Children.Add(value);
        }
    }
}
