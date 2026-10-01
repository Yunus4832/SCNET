namespace Game.Widgets;

public class PageIndicatorWidget : Widget
{
    private const float _dotDiameter = 4f;

    private const float _dotSpacing = 5f;

    public int PageCount { get; set; }

    public int PageIndex { get; set; }

    public Color ActiveColor { get; set; } = new(255, 255, 255, 200);

    public Color InactiveColor { get; set; } = new(180, 180, 180, 80);

    public override bool IsHitTestVisible { get; set; } = false;

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = PageCount > 1;
        var width = PageCount * _dotDiameter + MathUtils.Max(PageCount - 1, 0) * _dotSpacing;
        DesiredSize = new Vector2(width, 10f);
    }

    public override void Draw(DrawContext dc)
    {
        var flatBatch = dc.PrimitivesRenderer2D.FlatBatch();
        var start = flatBatch.TriangleVertices.Count;
        var totalWidth = PageCount * _dotDiameter + MathUtils.Max(PageCount - 1, 0) * _dotSpacing;
        var center = new Vector2((ActualSize.X - totalWidth) / 2f + _dotDiameter / 2f, ActualSize.Y / 2f);
        for (var i = 0; i < PageCount; i++)
        {
            var color = i == PageIndex ? ActiveColor : InactiveColor;
            flatBatch.QueueDisc(center, new Vector2(_dotDiameter / 2f), 0f, color * GlobalColorTransform, 12);
            center.X += _dotDiameter + _dotSpacing;
        }

        flatBatch.TransformTriangles(GlobalTransform, start);
    }
}
