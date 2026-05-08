namespace Game.Widgets;

public class FixedSizePanelWidget : ContainerWidget
{
    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        var zero = Vector2.Zero;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            child.Measure(Vector2.Max(parentAvailableSize - 2f * child.Margin, Vector2.Zero));
            if (child.ParentDesiredSize.X.UncloseTo(float.PositiveInfinity))
            {
                zero.X = MathUtils.Max(zero.X, child.ParentDesiredSize.X + 2f * child.Margin.X);
            }

            if (child.ParentDesiredSize.Y.UncloseTo(float.PositiveInfinity))
            {
                zero.Y = MathUtils.Max(zero.Y, child.ParentDesiredSize.Y + 2f * child.Margin.Y);
            }
        }

        DesiredSize = zero;
    }

    public override void ArrangeOverride()
    {
        foreach (var child in Children)
        {
            ArrangeChildWidgetInCell(Vector2.Zero, ActualSize, child);
        }
    }
}
