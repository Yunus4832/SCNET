using Engine.Graphics;

namespace Game.Widgets;

public class PanoramaWidget : Widget
{
    private Vector2 _position;

    private readonly float _timeOffset = new Random().Float(0f, 1000f);

    public Texture2D Texture { get; set; } = AssetManager.Get<Texture2D>("Textures/Panorama");

    public void DrawImage(DrawContext dc)
    {
        var animationTime = (float)MathUtils.Remainder(Time.FrameStartTime + _timeOffset, 10000.0);
        var x = 2f * SimplexNoise.OctavedNoise(animationTime, 0.02f, 4, 2f, 0.5f) - 1f;
        var y = 2f * SimplexNoise.OctavedNoise(animationTime + 100f, 0.02f, 4, 2f, 0.5f) - 1f;
        _position += 0.03f * new Vector2(x, y) * MathUtils.Min(Time.FrameDuration, 0.1f);
        _position.X = MathUtils.Remainder(_position.X, 1f);
        _position.Y = MathUtils.Remainder(_position.Y, 1f);
        var f = 0.5f * MathUtils.PowSign(MathUtils.Sin(0.21f * animationTime + 2f), 2f) + 0.5f;
        var halfU = MathUtils.Lerp(0.13f, 0.3f, f);
        var halfV = halfU / Texture.Height * Texture.Width / ActualSize.X * ActualSize.Y;
        var x2 = _position.X;
        var y2 = _position.Y;
        var zero = Vector2.Zero;
        var actualSize = ActualSize;
        var texCoord = new Vector2(x2 - halfU, y2 - halfV);
        var texCoord2 = new Vector2(x2 + halfU, y2 + halfV);
        var texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(
            Texture,
            false,
            0,
            DepthStencilState.DepthWrite,
            null,
            BlendState.AlphaBlend,
            SamplerState.LinearWrap
        );
        var count = texturedBatch2D.TriangleVertices.Count;
        texturedBatch2D.QueueQuad(zero, actualSize, 1f, texCoord, texCoord2, GlobalColorTransform);
        texturedBatch2D.TransformTriangles(GlobalTransform, count);
    }

    public void DrawSquares(DrawContext dc)
    {
        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None, null, BlendState.AlphaBlend);
        var count = flatBatch2D.LineVertices.Count;
        var count2 = flatBatch2D.TriangleVertices.Count;
        var animationTime = (float)MathUtils.Remainder(Time.FrameStartTime + _timeOffset, 10000.0);
        var squareSize = ActualSize.X / 12f;
        var opacity = GlobalColorTransform.A / 255f;
        for (var squareX = 0f; squareX < ActualSize.X; squareX += squareSize)
        {
            for (var squareY = 0f; squareY < ActualSize.Y; squareY += squareSize)
            {
                var outlineOpacity = 0.35f *
                                     MathUtils.Pow(
                                         MathUtils.Saturate(
                                             SimplexNoise.OctavedNoise(
                                                 squareX + 1000f,
                                                 squareY,
                                                 0.7f * animationTime,
                                                 0.5f,
                                                 1,
                                                 2f,
                                                 1f
                                             ) -
                                             0.1f
                                         ),
                                         1f
                                     ) *
                                     opacity;
                var fillOpacity =
                    0.7f *
                    MathUtils.Pow(
                        SimplexNoise.OctavedNoise(squareX, squareY, 0.5f * animationTime, 0.5f, 1, 2f, 1f),
                        3f
                    ) *
                    opacity;
                var corner = new Vector2(squareX, squareY);
                var corner2 = new Vector2(squareX + squareSize, squareY + squareSize);
                if (outlineOpacity > 0.01f)
                {
                    flatBatch2D.QueueRectangle(corner, corner2, 0f, new Color(0f, 0f, 0f, outlineOpacity));
                }

                if (fillOpacity > 0.01f)
                {
                    flatBatch2D.QueueQuad(corner, corner2, 0f, new Color(0f, 0f, 0f, fillOpacity));
                }
            }
        }

        flatBatch2D.TransformLines(GlobalTransform, count);
        flatBatch2D.TransformTriangles(GlobalTransform, count2);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
    }

    public override void Draw(DrawContext dc)
    {
        DrawImage(dc);
        DrawSquares(dc);
    }
}
