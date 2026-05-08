namespace Game.Widgets;

public class UniformSpacingPanelWidget : ContainerWidget
{
    private int _count;

    private LayoutDirection _direction;

    public LayoutDirection Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public override void ArrangeOverride()
    {
        var zero = Vector2.Zero;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                if (_direction == LayoutDirection.Horizontal)
                {
                    var num = _count > 0 ? ActualSize.X / _count : 0f;
                    ArrangeChildWidgetInCell(zero, new Vector2(zero.X + num, zero.Y + ActualSize.Y), child);
                    zero.X += num;
                }
                else
                {
                    var num2 = _count > 0 ? ActualSize.Y / _count : 0f;
                    ArrangeChildWidgetInCell(zero, new Vector2(zero.X + ActualSize.X, zero.Y + num2), child);
                    zero.Y += num2;
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
        var num = 0f;
        foreach (var child2 in Children)
        {
            if (child2.IsVisible)
            {
                child2.Measure(Vector2.Max(parentAvailableSize - 2f * child2.Margin, Vector2.Zero));
                num = _direction != 0
                    ? MathUtils.Max(num, child2.ParentDesiredSize.X + 2f * child2.Margin.X)
                    : MathUtils.Max(num, child2.ParentDesiredSize.Y + 2f * child2.Margin.Y);
            }
        }

        DesiredSize = _direction == LayoutDirection.Horizontal
            ? new Vector2(float.PositiveInfinity, num)
            : new Vector2(num, float.PositiveInfinity);
    }
}
