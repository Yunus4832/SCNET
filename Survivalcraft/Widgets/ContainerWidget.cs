namespace Game.Widgets;

public abstract class ContainerWidget : Widget
{
    public readonly WidgetsList Children;

    protected ContainerWidget()
    {
        Children = new WidgetsList(this);
    }

    public IEnumerable<Widget?> AllChildren
    {
        get
        {
            foreach (var childWidget in Children)
            {
                yield return childWidget;
                if (childWidget is not ContainerWidget containerWidget)
                {
                    continue;
                }

                foreach (var allChild in containerWidget.AllChildren)
                {
                    yield return allChild;
                }
            }
        }
    }

    public override void UpdateCeases()
    {
        foreach (var child in Children)
        {
            child.UpdateCeases();
        }
    }

    public void AddChildren(Widget widget)
    {
        if (Children.IndexOf(widget) < 0)
        {
            Children.Add(widget);
        }
    }

    public void RemoveChildren(Widget widget)
    {
        if (Children.IndexOf(widget) >= 0)
        {
            Children.Remove(widget);
        }
    }

    protected void ClearChildren()
    {
        Children.Clear();
    }

    public virtual void WidgetAdded(Widget widget)
    {
    }

    public virtual void WidgetRemoved(Widget widget)
    {
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Vector2.Max(parentAvailableSize - 2f * child.Margin, Vector2.Zero));
        }
    }

    public override void ArrangeOverride()
    {
        foreach (var child in Children)
        {
            ArrangeChildWidgetInCell(Vector2.Zero, ActualSize, child);
        }
    }

    protected static void ArrangeChildWidgetInCell(Vector2 startPoint, Vector2 endPoint, Widget child)
    {
        var position = Vector2.Zero;
        var parentActualSize = Vector2.Zero;
        var vector = endPoint - startPoint;
        var margin = child.Margin;
        var parentDesiredSize = child.ParentDesiredSize;
        if (float.IsPositiveInfinity(parentDesiredSize.X) || parentDesiredSize.X > vector.X - 2f * margin.X)
        {
            parentDesiredSize.X = MathUtils.Max(vector.X - 2f * margin.X, 0f);
        }

        if (float.IsPositiveInfinity(parentDesiredSize.Y) || parentDesiredSize.Y > vector.Y - 2f * margin.Y)
        {
            parentDesiredSize.Y = MathUtils.Max(vector.Y - 2f * margin.Y, 0f);
        }

        if (child.HorizontalAlignment == WidgetAlignment.Near)
        {
            position.X = startPoint.X + margin.X;
            parentActualSize.X = parentDesiredSize.X;
        }
        else if (child.HorizontalAlignment == WidgetAlignment.Center)
        {
            position.X = startPoint.X + (vector.X - parentDesiredSize.X) / 2f;
            parentActualSize.X = parentDesiredSize.X;
        }
        else if (child.HorizontalAlignment == WidgetAlignment.Far)
        {
            position.X = endPoint.X - parentDesiredSize.X - margin.X;
            parentActualSize.X = parentDesiredSize.X;
        }
        else if (child.HorizontalAlignment == WidgetAlignment.Stretch)
        {
            position.X = startPoint.X + margin.X;
            parentActualSize.X = MathUtils.Max(vector.X - 2f * margin.X, 0f);
        }

        if (child.VerticalAlignment == WidgetAlignment.Near)
        {
            position.Y = startPoint.Y + margin.Y;
            parentActualSize.Y = parentDesiredSize.Y;
        }
        else if (child.VerticalAlignment == WidgetAlignment.Center)
        {
            position.Y = startPoint.Y + (vector.Y - parentDesiredSize.Y) / 2f;
            parentActualSize.Y = parentDesiredSize.Y;
        }
        else if (child.VerticalAlignment == WidgetAlignment.Far)
        {
            position.Y = endPoint.Y - parentDesiredSize.Y - margin.Y;
            parentActualSize.Y = parentDesiredSize.Y;
        }
        else if (child.VerticalAlignment == WidgetAlignment.Stretch)
        {
            position.Y = startPoint.Y + margin.Y;
            parentActualSize.Y = MathUtils.Max(vector.Y - 2f * margin.Y, 0f);
        }

        child.Arrange(position, parentActualSize);
    }

    public override void Dispose()
    {
        foreach (var child in Children)
        {
            child.Dispose();
        }
    }
}
