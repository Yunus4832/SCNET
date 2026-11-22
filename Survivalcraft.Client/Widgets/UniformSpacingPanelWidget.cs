namespace Game.Widgets;

public class UniformSpacingPanelWidget : ContainerWidget
{
    private int _count;

    private LayoutDirection _direction;

    public LayoutDirection Direction { get => _direction; set => _direction = value; }

    public override void ArrangeOverride()
    {
        var zero = Vector2.Zero;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                if (_direction == LayoutDirection.Horizontal)
                {
                    var cellWidth = _count > 0 ? ActualSize.X / _count : 0f;
                    ArrangeChildWidgetInCell(zero, new Vector2(zero.X + cellWidth, zero.Y + ActualSize.Y), child);
                    zero.X += cellWidth;
                }
                else
                {
                    var cellHeight = _count > 0 ? ActualSize.Y / _count : 0f;
                    ArrangeChildWidgetInCell(zero, new Vector2(zero.X + ActualSize.X, zero.Y + cellHeight), child);
                    zero.Y += cellHeight;
                }
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        _count = 0;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                _count++;
            }
        }

        parentAvailableSize = _direction != 0
            ? Vector2.Min(parentAvailableSize, new Vector2(parentAvailableSize.X, parentAvailableSize.Y / _count))
            : Vector2.Min(parentAvailableSize, new Vector2(parentAvailableSize.X / _count, parentAvailableSize.Y));
        var crossAxisSize = 0f;
        foreach (var child2 in Children)
        {
            if (child2.IsVisible)
            {
                child2.Measure(Vector2.Max(parentAvailableSize - 2f * child2.Margin, Vector2.Zero));
                crossAxisSize = _direction != 0
                    ? MathUtils.Max(crossAxisSize, child2.ParentDesiredSize.X + 2f * child2.Margin.X)
                    : MathUtils.Max(crossAxisSize, child2.ParentDesiredSize.Y + 2f * child2.Margin.Y);
            }
        }

        DesiredSize = _direction == LayoutDirection.Horizontal
            ? new Vector2(float.PositiveInfinity, crossAxisSize)
            : new Vector2(crossAxisSize, float.PositiveInfinity);
    }
}
