using Engine.Graphics;

namespace Game.Widgets;

public class ImageWidget : CanvasWidget
{
    public Color? Background;

    public Vector2 EndPoint;

    public Vector2 Padding;

    public Vector2 StartPoint;

    public Texture2D? Texture { set; get; }

    public Subtexture SubTexture
    {
        set
        {
            Texture = value.Texture;
            StartPoint = value.TopLeft;
            EndPoint = value.BottomRight;
        }
    }

    public float RotateAngle { get; set; }

    public Color ColorTransForm { get; set; }

    public ImageWidget()
    {
        IsDrawRequired = true;
        ColorTransForm = Color.White;
    }


    public override void Draw(DrawContext dc)
    {
        if (Texture == null)
        {
            return;
        }

        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch();
        var texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(Texture, true);
        var startF = flatBatch2D.TriangleVertices.Count;
        var startT = texturedBatch2D.TriangleVertices.Count;
        texturedBatch2D.QueueQuad(Vector2.Zero + Padding, Size, 1f, StartPoint, EndPoint, ColorTransForm);
        texturedBatch2D.TransformTriangles(
            Matrix.CreateTranslation(-Size.X / 2f, -Size.Y / 2f, 0f) * Matrix.CreateRotationZ(RotateAngle) *
            Matrix.CreateTranslation(Size.X / 2f, Size.Y / 2f, 0f) * GlobalTransform, startT);
        if (!Background.HasValue)
        {
            return;
        }

        flatBatch2D.QueueQuad(Vector2.Zero, Size, 0f, Background.Value);
        flatBatch2D.TransformTriangles(GlobalTransform, startF);
    }
}
