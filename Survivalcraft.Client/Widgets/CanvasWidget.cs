namespace Game.Widgets;

public class CanvasWidget : ContainerWidget
{
    private readonly Dictionary<Widget, Vector2> _positions = new();

    public Vector2 Size { get; set; } = new(-1f);

    public static void SetPosition(Widget widget, Vector2 position)
    {
        (widget.ParentWidget as CanvasWidget)?.SetWidgetPosition(widget, position);
    }

    public Vector2? GetWidgetPosition(Widget widget)
    {
        if (_positions.TryGetValue(widget, out var value))
        {
            return value;
        }

        return null;
    }

    public void SetWidgetPosition(Widget widget, Vector2? position)
    {
        if (position.HasValue)
        {
            _positions[widget] = position.Value;
        }
        else
        {
            _positions.Remove(widget);
        }
    }

    public override void WidgetRemoved(Widget widget)
    {
        _positions.Remove(widget);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var desiredSize = Vector2.Zero;
        if (Size.X >= 0f)
        {
            parentAvailableSize.X = MathUtils.Min(parentAvailableSize.X, Size.X);
        }

        if (Size.Y >= 0f)
        {
            parentAvailableSize.Y = MathUtils.Min(parentAvailableSize.Y, Size.Y);
        }

        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                var widgetPosition = GetWidgetPosition(child);
                var v = widgetPosition ?? Vector2.Zero;
                child.Measure(Vector2.Max(parentAvailableSize - v - 2f * child.Margin, Vector2.Zero));
                Vector2 vector = default;
                vector.X = MathUtils.Max(desiredSize.X, v.X + child.ParentDesiredSize.X + 2f * child.Margin.X);
                vector.Y = MathUtils.Max(desiredSize.Y, v.Y + child.ParentDesiredSize.Y + 2f * child.Margin.Y);
                desiredSize = vector;
            }
        }

        if (Size.X >= 0f)
        {
            desiredSize.X = Size.X;
        }

        if (Size.Y >= 0f)
        {
            desiredSize.Y = Size.Y;
        }

        DesiredSize = desiredSize;
    }

    public override void ArrangeOverride()
    {
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                var widgetPosition = GetWidgetPosition(child);
                if (widgetPosition.HasValue)
                {
                    var parentActualSize = Vector2.Zero;
                    parentActualSize.X = !float.IsPositiveInfinity(child.ParentDesiredSize.X)
                        ? child.ParentDesiredSize.X
                        : MathUtils.Max(ActualSize.X - widgetPosition.Value.X, 0f);
                    parentActualSize.Y = !float.IsPositiveInfinity(child.ParentDesiredSize.Y)
                        ? child.ParentDesiredSize.Y
                        : MathUtils.Max(ActualSize.Y - widgetPosition.Value.Y, 0f);
                    child.Arrange(widgetPosition.Value, parentActualSize);
                }
                else
                {
                    ArrangeChildWidgetInCell(Vector2.Zero, ActualSize, child);
                }
            }
        }
    }
}
