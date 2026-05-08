using Engine.Graphics;

namespace Game.Widgets;

public class ScrollPanelWidget : ContainerWidget
{
    private float _dragSpeed;

    private Vector2? _lastDragPosition;

    public float ScrollAreaLength;

    private float _scrollBarAlpha;

    public ScrollPanelWidget()
    {
        ClampToBounds = true;
        StartInitialScroll();
    }

    public virtual LayoutDirection Direction { get; set; }

    public virtual float ScrollPosition { get; set; }

    public virtual float ScrollSpeed { get; set; }

    public void StartInitialScroll()
    {
        ScrollPosition = 12f;
        ScrollSpeed = -70f;
    }

    public virtual float CalculateScrollAreaLength()
    {
        var num = 0f;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                num = Direction != 0
                    ? MathUtils.Max(num, child.ParentDesiredSize.Y + 2f * child.Margin.Y)
                    : MathUtils.Max(num, child.ParentDesiredSize.X + 2f * child.Margin.X);
            }
        }

        return num;
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
                    child.Measure(new Vector2(float.MaxValue,
                        MathUtils.Max(parentAvailableSize.Y - 2f * child.Margin.Y, 0f)));
                }
                else
                {
                    child.Measure(new Vector2(MathUtils.Max(parentAvailableSize.X - 2f * child.Margin.X, 0f),
                        float.MaxValue));
                }
            }
        }
    }

    public override void ArrangeOverride()
    {
        foreach (var child in Children)
        {
            var zero = Vector2.Zero;
            var actualSize = ActualSize;
            if (Direction == LayoutDirection.Horizontal)
            {
                zero.X -= ScrollPosition;
                actualSize.X = zero.X + child.ParentDesiredSize.X;
            }
            else
            {
                zero.Y -= ScrollPosition;
                actualSize.Y = zero.Y + child.ParentDesiredSize.Y;
            }

            ArrangeChildWidgetInCell(zero, actualSize, child);
        }
    }

    public override void Update()
    {
        var num = 50f;
        ScrollAreaLength = CalculateScrollAreaLength();
        _scrollBarAlpha = MathUtils.Max(_scrollBarAlpha - 2f * Time.FrameDuration, 0f);
        if (Input.Tap.HasValue && HitTestPanel(Input.Tap.Value))
        {
            _lastDragPosition = ScreenToWidget(Input.Tap.Value);
        }

        if (_lastDragPosition.HasValue)
        {
            if (Input.Press.HasValue)
            {
                var vector = ScreenToWidget(Input.Press.Value);
                var vector2 = vector - _lastDragPosition.Value;
                float num2;
                if (Direction == LayoutDirection.Horizontal)
                {
                    ScrollPosition += 0f - vector2.X;
                    num2 = vector2.X / Time.FrameDuration;
                }
                else
                {
                    ScrollPosition += 0f - vector2.Y;
                    num2 = vector2.Y / Time.FrameDuration;
                }

                var num3 = MathUtils.Abs(num2) < MathUtils.Abs(_dragSpeed) ? 20f : 16f;
                _dragSpeed += MathUtils.Saturate(num3 * Time.FrameDuration) * (num2 - _dragSpeed);
                _scrollBarAlpha = 4f;
                _lastDragPosition = vector;
                ScrollSpeed = 0f;
            }
            else
            {
                ScrollSpeed = 0f - _dragSpeed;
                _dragSpeed = 0f;
                _lastDragPosition = null;
            }
        }

        if (ScrollSpeed != 0f)
        {
            ScrollSpeed *= MathUtils.Pow(0.33f, Time.FrameDuration);
            if (MathUtils.Abs(ScrollSpeed) < 40f)
            {
                ScrollSpeed = 0f;
            }

            ScrollPosition += ScrollSpeed * Time.FrameDuration;
            _scrollBarAlpha = 3f;
        }

        if (Input.Scroll.HasValue && HitTestPanel(Input.Scroll.Value.XY))
        {
            ScrollPosition -= 40f * Input.Scroll.Value.Z;
            ScrollSpeed = 0f;
            num = 0f;
            _scrollBarAlpha = 3f;
        }

        var num4 = MathUtils.Max(ScrollAreaLength - ActualSize.Y, 0f);
        if (ScrollPosition < 0f)
        {
            if (!_lastDragPosition.HasValue)
            {
                ScrollPosition = MathUtils.Min(ScrollPosition + 6f * Time.FrameDuration * (0f - ScrollPosition + 5f),
                    0f);
            }

            ScrollPosition = MathUtils.Max(ScrollPosition, 0f - num);
            ScrollSpeed = 0f;
        }

        if (ScrollPosition > num4)
        {
            if (!_lastDragPosition.HasValue && !float.IsPositiveInfinity(ScrollPosition))
            {
                ScrollPosition = MathUtils.Max(ScrollPosition + 6f * Time.FrameDuration * (num4 - ScrollPosition - 5f),
                    num4);
            }

            ScrollPosition = MathUtils.Min(ScrollPosition, num4 + num);
            ScrollSpeed = 0f;
        }

        if (_lastDragPosition.HasValue && (Input.Drag.HasValue || Input.Hold.HasValue))
        {
            Input.Clear();
        }
    }

    public override void Draw(DrawContext dc)
    {
        var color = new Color((byte)128, (byte)128, (byte)128) * GlobalColorTransform *
                    MathUtils.Saturate(_scrollBarAlpha);
        if (color.A <= 0 || !(ScrollAreaLength > 0f))
        {
            return;
        }

        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
        var count = flatBatch2D.TriangleVertices.Count;
        if (Direction == LayoutDirection.Horizontal)
        {
            var scrollPosition = ScrollPosition;
            var x = ActualSize.X;
            var corner = new Vector2(scrollPosition / ScrollAreaLength * x, ActualSize.Y - 5f);
            var corner2 = new Vector2((scrollPosition + x) / ScrollAreaLength * x, ActualSize.Y - 1f);
            flatBatch2D.QueueQuad(corner, corner2, 0f, color);
        }
        else
        {
            var scrollPosition2 = ScrollPosition;
            var y = ActualSize.Y;
            var corner3 = new Vector2(ActualSize.X - 5f, scrollPosition2 / ScrollAreaLength * y);
            var corner4 = new Vector2(ActualSize.X - 1f, (scrollPosition2 + y) / ScrollAreaLength * y);
            flatBatch2D.QueueQuad(corner3, corner4, 0f, color);
        }

        flatBatch2D.TransformTriangles(GlobalTransform, count);
    }

    public bool HitTestPanel(Vector2 position)
    {
        var found = false;
        HitTestGlobal(position, delegate(Widget widget)
        {
            found = widget.IsChildWidgetOf(this) || widget == this;
            return true;
        });
        return found;
    }
}
