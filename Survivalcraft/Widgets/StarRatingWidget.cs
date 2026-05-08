using Engine.Graphics;

namespace Game.Widgets;

public class StarRatingWidget : Widget
{
    private readonly Texture2D _texture = ContentManager.Get<Texture2D>("Textures/Gui/RatingStar");

    public float StarSize { get; set; } = 64f;

    public Color ForeColor { get; set; } = new(255, 192, 0);

    public Color BackColor { get; set; } = new(96, 96, 96);

    public float Rating
    {
        get;
        set => field = MathUtils.Clamp(value, 0f, 5f);
    }

    public override void Update()
    {
        if (!Input.Press.HasValue || HitTestGlobal(Input.Press.Value) != this)
        {
            return;
        }

        var vector = ScreenToWidget(Input.Press.Value);
        Rating = (int)MathUtils.Floor(5f * vector.X / ActualSize.X + 1f);
    }

    public override void Draw(DrawContext dc)
    {
        var texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(_texture, false, 0, DepthStencilState.None, null,
            null, SamplerState.LinearWrap);
        var x = 0f;
        var x2 = ActualSize.X * Rating / 5f;
        var x3 = ActualSize.X;
        var y = 0f;
        var y2 = ActualSize.Y;
        var count = texturedBatch2D.TriangleVertices.Count;
        texturedBatch2D.QueueQuad(new Vector2(x, y), new Vector2(x2, y2), 0f, new Vector2(0f, 0f),
            new Vector2(Rating, 1f), ForeColor * GlobalColorTransform);
        texturedBatch2D.QueueQuad(new Vector2(x2, y), new Vector2(x3, y2), 0f, new Vector2(Rating, 0f),
            new Vector2(5f, 1f), BackColor * GlobalColorTransform);
        texturedBatch2D.TransformTriangles(GlobalTransform, count);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        DesiredSize = new Vector2(5f * StarSize, StarSize);
    }
}
