namespace Game.Widgets;

public class DragHostWidget : ContainerWidget
{
    private IDragTargetWidget? _lastDragHitWidget;

    private object _dragData = DragDataDefault.Default;

    private Action? _dragEndedHandler;

    private Vector2 _dragPosition;

    private Widget? _dragWidget;

    public override bool IsHitTestVisible { get; set; } = false;

    public bool IsDragInProgress => _dragWidget != null;

    public void BeginDrag(Widget dragWidget, object dragData, Action dragEndedHandler)
    {
        if (_dragWidget != null)
        {
            return;
        }

        _dragWidget = dragWidget;
        _dragData = dragData;
        _dragEndedHandler = dragEndedHandler;
        Children.Add(_dragWidget);
        UpdateDragPosition();
    }

    public void EndDrag()
    {
        if (_dragWidget == null)
        {
            return;
        }

        Children.Remove(_dragWidget);
        _dragWidget = null;
        _dragData = DragDataDefault.Default;
        if (_dragEndedHandler == null)
        {
            return;
        }

        _dragEndedHandler();
        _dragEndedHandler = null;
    }

    public override void Update()
    {
        if (_dragWidget == null)
        {
            return;
        }

        UpdateDragPosition();
        var dragTargetWidget =
            HitTestGlobal(_dragPosition, w => w is IDragTargetWidget) as IDragTargetWidget;
        if (_lastDragHitWidget != dragTargetWidget)
        {
            _lastDragHitWidget?.DragOut(_dragWidget, _dragData);
            _lastDragHitWidget = dragTargetWidget;
            dragTargetWidget?.DragIn(_dragWidget, _dragData);
        }

        if (Input.Drag.HasValue)
        {
            dragTargetWidget?.DragOver(_dragWidget, _dragData);
        }
        else
        {
            try
            {
                dragTargetWidget?.DragDrop(_dragWidget, _dragData);
            }
            finally
            {
                EndDrag();
            }
        }
    }

    public override void ArrangeOverride()
    {
        foreach (var child in Children)
        {
            var parentDesiredSize = child.ParentDesiredSize;
            parentDesiredSize.X = MathUtils.Min(parentDesiredSize.X, ActualSize.X);
            parentDesiredSize.Y = MathUtils.Min(parentDesiredSize.Y, ActualSize.Y);
            child.Arrange(ScreenToWidget(_dragPosition) - 0.5f * parentDesiredSize, parentDesiredSize);
        }
    }

    public void UpdateDragPosition()
    {
        if (!Input.Drag.HasValue)
        {
            return;
        }

        _dragPosition = Input.Drag.Value;
        _dragPosition.X = MathUtils.Clamp(_dragPosition.X, GlobalBounds.Min.X, GlobalBounds.Max.X - 1f);
        _dragPosition.Y = MathUtils.Clamp(_dragPosition.Y, GlobalBounds.Min.Y, GlobalBounds.Max.Y - 1f);
    }
}
