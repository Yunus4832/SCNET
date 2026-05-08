using Engine.Graphics;

namespace Game.Widgets;

public class ClickTextWidget : CanvasWidget
{
    public readonly Action Click;

    public readonly LabelWidget LabelWidget;

    public Color BorderColor = Color.Transparent;

    public Color PressColor = Color.Red;

    public RectangleWidget? RectangleWidget;

    public override WidgetAlignment HorizontalAlignment { get; set; } = WidgetAlignment.Center;

    public override WidgetAlignment VerticalAlignment { get; set; } = WidgetAlignment.Center;

    public ClickTextWidget(Vector2 vector2, string text, Action click, bool box = false)
    {
        Size = vector2;
        LabelWidget = new LabelWidget
        {
            Text = text, FontScale = 0.8f, HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        Children.Add(LabelWidget);
        IsDrawEnabled = true;
        IsDrawRequired = true;
        IsUpdateEnabled = true;
        Click = click;
    }

    public override void Draw(DrawContext dc)
    {
        var m = GlobalTransform;
        var v = Vector2.Zero;
        var v2 = new Vector2(ActualSize.X, 0f);
        var v3 = ActualSize;
        var v4 = new Vector2(0f, ActualSize.Y);
        Vector2.Transform(ref v, ref m, out var result);
        Vector2.Transform(ref v2, ref m, out var result2);
        Vector2.Transform(ref v3, ref m, out var result3);
        Vector2.Transform(ref v4, ref m, out var result4);
        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.DepthWrite);
        var vector = Vector2.Normalize(GlobalTransform.Right.XY);
        var v5 = -Vector2.Normalize(GlobalTransform.Up.XY);
        for (var i = 0; i < 1; i++)
        {
            flatBatch2D.QueueLine(result, result2, 1f, BorderColor);
            flatBatch2D.QueueLine(result2, result3, 1f, BorderColor);
            flatBatch2D.QueueLine(result3, result4, 1f, BorderColor);
            flatBatch2D.QueueLine(result4, result, 1f, BorderColor);
            result += vector - v5;
            result2 += -vector - v5;
            result3 += -vector + v5;
            result4 += vector + v5;
        }
    }

    public override void Update()
    {
        if (Input.Click.HasValue && HitTest(Input.Click.Value.Start) && HitTest(Input.Click.Value.End))
        {
            Click?.Invoke();
        }
    }
}
