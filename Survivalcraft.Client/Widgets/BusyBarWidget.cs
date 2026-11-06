namespace Game.Widgets;

public class BusyBarWidget : Widget
{
    private const int _barsCount = 5;

    private const float _barSize = 8f;

    private const float _barsSpacing = 24f;

    private int _boxIndex;

    private double _lastBoxesStepTime;

    public override bool IsHitTestVisible { get; set; } = false;

    public Color LitBarColor { get; set; } = new(16, 140, 0);

    public Color UnlitBarColor { get; set; } = new(48, 48, 48);

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        DesiredSize = new Vector2(120f, 12f);
    }

    public override void Draw(DrawContext dc)
    {
        if (Time.RealTime - _lastBoxesStepTime > 0.25)
        {
            _boxIndex++;
            _lastBoxesStepTime = Time.RealTime;
        }

        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch();
        var count = flatBatch2D.TriangleVertices.Count;
        for (var i = 0; i < 5; i++)
        {
            var v = new Vector2((i + 0.5f) * 24f, 6f);
            var c = i == _boxIndex % 5 ? LitBarColor : UnlitBarColor;
            var v2 = i == _boxIndex % 5 ? 12f : 8f;
            flatBatch2D.QueueQuad(v - new Vector2(v2) / 2f, v + new Vector2(v2) / 2f, 0f, c * GlobalColorTransform);
        }

        flatBatch2D.TransformTriangles(GlobalTransform, count);
    }
}
