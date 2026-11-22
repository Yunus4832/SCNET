using Engine.Graphics;

namespace Game.Widgets;

public class ResizableListWidget : ScrollPanelWidget
{
    private readonly List<float> _itemWidgetSize = [];

    private Vector2 _lastActualSize = new(-1f);

    private bool _clickAllowed;

    private int _firstVisibleIndex;

    private readonly List<object> _items = [];

    private int _lastVisibleIndex;

    private int _selectedItemIndex = -1;

    private readonly Dictionary<int, Widget> _widgetsByIndex = new();

    private bool _widgetsDirty;

    public Func<object, Widget> ItemWidgetFactory { get; set; }

    public bool KeepItemsWholeWhenScrolling { get; set; }

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

            var itemIndex = _items.IndexOf(value);
            SelectedIndex = itemIndex;
        }
    }

    public ReadOnlyList<object> Items => new(_items);

    public Color SelectionColor { get; set; }

    public event Action<object>? ItemClicked;

    public event Action? SelectionChanged;

    public ResizableListWidget()
    {
        SelectionColor = Color.Gray;
        ItemWidgetFactory = item => new LabelWidget
        {
            Text = item.ToString() ?? string.Empty,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
    }


    public void AddItem(object item)
    {
        _items.Add(item);
        var w = ItemWidgetFactory.Invoke(item);
        w.Measure(
            Direction == LayoutDirection.Horizontal
                ? new Vector2(0f, ActualSize.Y)
                : new Vector2(ActualSize.X, 0f)
        );

        var previous = _itemWidgetSize.Count == 0 ? 0f : _itemWidgetSize[^1];
        _itemWidgetSize.Add(
            previous +
            (Direction == LayoutDirection.Horizontal ? w.ParentDesiredSize.X : w.ParentDesiredSize.Y)
        );
        _widgetsDirty = true;
    }

    public void RemoveItem(object item)
    {
        var itemIndex = _items.IndexOf(item);
        if (itemIndex >= 0)
        {
            RemoveItemAt(itemIndex);
        }
    }

    public void RemoveItemAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        Widget w;
        if (_widgetsByIndex.TryGetValue(index, out var widget))
        {
            w = widget;
        }
        else
        {
            w = ItemWidgetFactory.Invoke(item);
            w.Measure(Vector2.Zero);
        }

        _itemWidgetSize.RemoveAt(index);
        for (var i = index; i < _itemWidgetSize.Count; i++)
        {
            _itemWidgetSize[i] -= Direction == LayoutDirection.Horizontal ? w.DesiredSize.X : w.DesiredSize.Y;
        }

        _widgetsByIndex.Clear();
        _widgetsDirty = true;
        if (index == SelectedIndex)
        {
            SelectedIndex = null;
        }
    }

    public void ClearItems()
    {
        _items.Clear();
        _itemWidgetSize.Clear();
        _widgetsByIndex.Clear();
        _widgetsDirty = true;
        SelectedIndex = null;
    }

    public override float CalculateScrollAreaLength()
    {
        if (_itemWidgetSize.Count == 0)
        {
            return 0f;
        }

        var length = _itemWidgetSize[^1];
        if (KeepItemsWholeWhenScrolling)
        {
            var viewportLength = Direction == LayoutDirection.Horizontal ? ActualSize.X : ActualSize.Y;
            length += MathUtils.Max(viewportLength - GetItemSize(_itemWidgetSize.Count - 1), 0f);
        }

        return length;
    }

    private float GetItemPosition(int num)
    {
        return num == 0 ? 0 : _itemWidgetSize[num - 1];
    }

    private float GetItemSize(int num)
    {
        return _itemWidgetSize[num] - (num == 0 ? 0f : _itemWidgetSize[num - 1]);
    }

    public void ScrollToPosition(int num)
    {
        if (num >= 0 && num < _items.Count)
        {
            var itemStart = GetItemPosition(num);
            var viewportLength = Direction == LayoutDirection.Horizontal ? ActualSize.X : ActualSize.Y;
            if (itemStart < ScrollPosition)
            {
                ScrollPosition = itemStart;
            }
            else
            {
                var itemSize = GetItemSize(num);
                if (itemStart > ScrollPosition + viewportLength - itemSize)
                {
                    var scrollPosition = itemStart - viewportLength + itemSize;
                    if (KeepItemsWholeWhenScrolling && itemSize <= viewportLength)
                    {
                        for (var i = 0; i <= num; i++)
                        {
                            var itemPosition = GetItemPosition(i);
                            if (itemPosition >= scrollPosition)
                            {
                                scrollPosition = itemPosition;
                                break;
                            }
                        }
                    }

                    ScrollPosition = scrollPosition;
                }
            }
        }
    }

    public void ScrollToItem(object item)
    {
        var itemIndex = _items.IndexOf(item);
        ScrollToPosition(itemIndex);
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
                    child.Measure(new Vector2(0f, MathUtils.Max(parentAvailableSize.Y - 2f * child.Margin.Y, 0f)));
                }
                else
                {
                    child.Measure(new Vector2(MathUtils.Max(parentAvailableSize.X - 2f * child.Margin.X, 0f), 0f));
                }
            }
        }

        if (!_widgetsDirty)
        {
            return;
        }

        _widgetsDirty = false;
        _itemWidgetSize.Clear();
        for (var i = 0; i < _items.Count; i++)
        {
            Widget w;
            if (_widgetsByIndex.TryGetValue(i, out var widget))
            {
                w = widget;
            }
            else
            {
                w = ItemWidgetFactory.Invoke(_items[i]);
                w.Measure(Vector2.Zero);
            }

            w.Measure(
                Direction == LayoutDirection.Horizontal
                    ? new Vector2(0f, ActualSize.Y)
                    : new Vector2(ActualSize.X, 0f)
            );

            var previous = _itemWidgetSize.Count == 0 ? 0f : _itemWidgetSize[^1];
            _itemWidgetSize.Add(
                previous +
                (Direction == LayoutDirection.Horizontal
                    ? w.ParentDesiredSize.X
                    : w.ParentDesiredSize.Y)
            );
        }

        CreateListWidgets(Direction == LayoutDirection.Horizontal ? ActualSize.X : ActualSize.Y);
    }

    public override void ArrangeOverride()
    {
        if (ActualSize != _lastActualSize)
        {
            _widgetsDirty = true;
        }

        _lastActualSize = ActualSize;
        var itemIndex = _firstVisibleIndex;
        foreach (var child in Children)
        {
            var itemSize = GetItemSize(itemIndex);
            var itemPos = GetItemPosition(itemIndex);
            if (Direction == LayoutDirection.Horizontal)
            {
                var vector = new Vector2(itemPos - ScrollPosition, 0f);
                ArrangeChildWidgetInCell(vector, vector + new Vector2(itemSize, ActualSize.Y), child);
            }
            else
            {
                var childPosition = new Vector2(0f, itemPos - ScrollPosition);
                ArrangeChildWidgetInCell(childPosition, childPosition + new Vector2(ActualSize.X, itemSize), child);
            }

            itemIndex++;
        }
    }

    public override void Update()
    {
        var wasScrolling = ScrollSpeed != 0f;
        base.Update();
        if (Input.Tap.HasValue && HitTestPanel(Input.Tap.Value))
        {
            _clickAllowed = !wasScrolling;
        }

        if (!Input.Click.HasValue ||
            !_clickAllowed ||
            !HitTestPanel(Input.Click.Value.Start) ||
            !HitTestPanel(Input.Click.Value.End))
        {
            return;
        }

        var clickedIndex = PositionToItemIndex(Input.Click.Value.End);
        if (ItemClicked != null && clickedIndex >= 0 && clickedIndex < _items.Count)
        {
            ItemClicked(Items[clickedIndex]);
        }

        SelectedIndex = clickedIndex;
        if (SelectedIndex.HasValue)
        {
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }
    }

    public override void Draw(DrawContext dc)
    {
        if (SelectedIndex.HasValue &&
            SelectedIndex.Value >= _firstVisibleIndex &&
            SelectedIndex.Value <= _lastVisibleIndex)
        {
            var itemSize = GetItemSize(SelectedIndex.Value);
            var vector = Direction == LayoutDirection.Horizontal
                ? new Vector2(GetItemPosition(SelectedIndex.Value) - ScrollPosition, 0f)
                : new Vector2(0f, GetItemPosition(SelectedIndex.Value) - ScrollPosition);
            var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
            var count = flatBatch2D.TriangleVertices.Count;
            var v = Direction == LayoutDirection.Horizontal
                ? new Vector2(itemSize, ActualSize.Y)
                : new Vector2(ActualSize.X, itemSize);
            flatBatch2D.QueueQuad(vector, vector + v, 0f, SelectionColor * GlobalColorTransform);
            flatBatch2D.TransformTriangles(GlobalTransform, count);
        }

        base.Draw(dc);
    }

    public int PositionToItemIndexInternal(float value)
    {
        var low = 0;
        var high = _itemWidgetSize.Count - 1;
        var middle = (low + high + 1) / 2;
        var location = -1;
        if (value < 0 || low >= _itemWidgetSize.Count || high >= _itemWidgetSize.Count)
        {
            return -1;
        }

        if (value < _itemWidgetSize[low])
        {
            return 0;
        }

        if (value > _itemWidgetSize[high])
        {
            return _itemWidgetSize.Count;
        }

        do
        {
            if (value < (middle == 0 ? 0 : _itemWidgetSize[middle - 1]))
            {
                high = MathUtils.Max(middle - 1, 0);
            }
            else if (value > _itemWidgetSize[middle])
            {
                low = middle;
            }
            else
            {
                location = middle;
            }

            middle = (low + high + 1) / 2;
        } while (low <= high && location == -1);

        return location;
    }

    public int PositionToItemIndex(Vector2 position)
    {
        var vector = ScreenToWidget(position);
        return Direction == LayoutDirection.Horizontal
            ? PositionToItemIndexInternal(vector.X + ScrollPosition)
            : PositionToItemIndexInternal(vector.Y + ScrollPosition);
    }

    public void CreateListWidgets(float size)
    {
        Children.Clear();
        if (_items.Count <= 0)
        {
            return;
        }

        var x = PositionToItemIndexInternal(ScrollPosition);
        var x2 = PositionToItemIndexInternal(ScrollPosition + size);
        _firstVisibleIndex = MathUtils.Max(x, 0);
        _lastVisibleIndex = MathUtils.Min(x2, _items.Count - 1);
        for (var i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var obj = _items[i];
            if (!_widgetsByIndex.TryGetValue(i, out var value))
            {
                value = ItemWidgetFactory(obj);
                value.Tag = obj;
                _widgetsByIndex.Add(i, value);
            }

            Children.Add(value);
        }
    }
}
