using Engine.Graphics;

namespace Game.Widgets;

public class BevelledRectangleWidget : Widget
{
    private readonly FlatBatch2D _cachedFlatBatch = new();

    private readonly FlatBatch2D _cachedShadowBatch = new();

    private readonly TexturedBatch2D _cachedTexturedBatch = new();

    private readonly BevelledShapeRenderer.Point[] _points = new BevelledShapeRenderer.Point[4];

    private bool _cachedBatchesValid;

    private float _cachedPixelsPerUnit;

    private Vector2 _cachedTextureScale;

    private FlatBatch2D? _flatBatch;

    private TexturedBatch2D? _texturedBatch;

    public override bool IsHitTestVisible { get; set; } = false;

    public Vector2 Size { get; set; }

    public float RoundingRadius
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            _cachedBatchesValid = false;
        }
    }

    public int RoundingCount
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _cachedBatchesValid = false;
        }
    }

    public float BevelSize
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            _cachedBatchesValid = false;
        }
    }

    public float DirectionalLight
    {
        get;
        set
        {
            if (value.UncloseTo(field))
            {
                field = value;
                _cachedBatchesValid = false;
            }
        }
    }

    public float AmbientLight
    {
        get;
        set
        {
            if (value.UncloseTo(field))
            {
                field = value;
                _cachedBatchesValid = false;
            }
        }
    }

    public Texture2D? Texture
    {
        get;
        set
        {
            if (!Equals(value, field))
            {
                field = value;
            }
        }
    }

    public float TextureScale { get; set; }

    public bool TextureLinearFilter
    {
        get;
        set
        {
            if (!value.Equals(field))
            {
                field = value;
            }
        }
    }

    public Color CenterColor
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;
                _cachedBatchesValid = false;
            }
        }
    }

    public Color BevelColor
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _cachedBatchesValid = false;
        }
    }

    public Color ShadowColor
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _cachedBatchesValid = false;
        }
    }

    public float ShadowSize
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            _cachedBatchesValid = false;
        }
    }

    public Vector2 TextureOffset => Vector2.Zero;


    public BevelledRectangleWidget()
    {
        Size = new Vector2(float.PositiveInfinity);
        TextureLinearFilter = false;
        TextureScale = 1f;
        RoundingRadius = 6f;
        RoundingCount = 3;
        BevelSize = 2f;
        AmbientLight = 0.6f;
        DirectionalLight = 0.4f;
        CenterColor = new Color(181, 172, 154);
        BevelColor = new Color(181, 172, 154);
        ShadowColor = new Color(0, 0, 0, 32);
        ShadowSize = 2f;
    }

    public override void Draw(DrawContext dc)
    {
        var centerColor = CenterColor * new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var bevelColor = BevelColor;
        var shadowColor = ShadowColor;
        var drawShadow = shadowColor != Color.Transparent && BevelSize > 0f;
        var globalScale = GlobalScale;
        if (globalScale.UncloseTo(_cachedPixelsPerUnit))
        {
            _cachedPixelsPerUnit = globalScale;
            _cachedBatchesValid = false;
        }

        var vector = new Vector2(TextureScale) / RootWidget.GlobalScale;
        if (vector != _cachedTextureScale)
        {
            _cachedTextureScale = vector;
            _cachedBatchesValid = false;
        }

        var antialiasSize = 1f / globalScale;
        if (Texture != null)
        {
            if (!_cachedBatchesValid)
            {
                var flatShading = _points.Any(p => p.RoundingCount == 0);
                _cachedShadowBatch.Clear();
                _cachedTexturedBatch.Clear();
                _cachedTexturedBatch.Texture = Texture;
                if (drawShadow)
                {
                    BevelledShapeRenderer.QueueShapeShadow(
                        _cachedShadowBatch,
                        _points,
                        globalScale,
                        ShadowSize,
                        shadowColor
                    );
                }

                BevelledShapeRenderer.QueueShape(
                    _cachedTexturedBatch,
                    _points,
                    vector,
                    TextureOffset,
                    globalScale,
                    antialiasSize,
                    BevelSize,
                    flatShading,
                    centerColor,
                    bevelColor,
                    DirectionalLight,
                    AmbientLight
                );
                _cachedBatchesValid = true;
            }

            if (drawShadow)
            {
                if (_flatBatch == null)
                {
                    _flatBatch = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
                }

                _flatBatch.QueueBatch(
                    _cachedShadowBatch,
                    Matrix.CreateTranslation(ShadowSize, ShadowSize, 0f) * GlobalTransform,
                    GlobalColorTransform
                );
            }

            if (_texturedBatch == null)
            {
                _texturedBatch = dc.PrimitivesRenderer2D.TexturedBatch(
                    Texture,
                    false,
                    1,
                    null,
                    null,
                    null,
                    TextureLinearFilter ? SamplerState.LinearWrap : SamplerState.PointWrap
                );
            }

            _texturedBatch.QueueBatch(_cachedTexturedBatch, GlobalTransform, GlobalColorTransform);
            return;
        }

        if (!_cachedBatchesValid)
        {
            var flatShading2 = _points.Any(p => p.RoundingCount == 0);
            _cachedShadowBatch.Clear();
            _cachedFlatBatch.Clear();
            if (drawShadow)
            {
                BevelledShapeRenderer.QueueShapeShadow(
                    _cachedShadowBatch,
                    _points,
                    globalScale,
                    ShadowSize,
                    shadowColor
                );
            }

            BevelledShapeRenderer.QueueShape(
                _cachedFlatBatch,
                _points,
                globalScale,
                antialiasSize,
                BevelSize,
                flatShading2,
                centerColor,
                bevelColor,
                DirectionalLight,
                AmbientLight
            );
            _cachedBatchesValid = true;
        }

        if (_flatBatch == null)
        {
            _flatBatch = dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None);
        }

        if (drawShadow)
        {
            _flatBatch.QueueBatch(
                _cachedShadowBatch,
                Matrix.CreateTranslation(ShadowSize, ShadowSize, 0f) * GlobalTransform,
                GlobalColorTransform
            );
        }

        _flatBatch.QueueBatch(_cachedFlatBatch, GlobalTransform, GlobalColorTransform);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = BevelColor.A != 0 || CenterColor.A != 0;
        DesiredSize = Size;
    }

    public override void ArrangeOverride()
    {
        var vector = new Vector2(0f, 0f);
        var topRight = new Vector2(ActualSize.X, 0f);
        var bottomRight = new Vector2(ActualSize.X, ActualSize.Y);
        var bottomLeft = new Vector2(0f, ActualSize.Y);
        if (vector != _points[0].Position ||
            topRight != _points[1].Position ||
            bottomRight != _points[2].Position ||
            bottomLeft != _points[3].Position)
        {
            _points[0] = new BevelledShapeRenderer.Point
            {
                Position = vector,
                RoundingRadius = RoundingRadius,
                RoundingCount = RoundingCount
            };
            _points[1] = new BevelledShapeRenderer.Point
            {
                Position = topRight,
                RoundingRadius = RoundingRadius,
                RoundingCount = RoundingCount
            };
            _points[2] = new BevelledShapeRenderer.Point
            {
                Position = bottomRight,
                RoundingRadius = RoundingRadius,
                RoundingCount = RoundingCount
            };
            _points[3] = new BevelledShapeRenderer.Point
            {
                Position = bottomLeft,
                RoundingRadius = RoundingRadius,
                RoundingCount = RoundingCount
            };
            _cachedBatchesValid = false;
        }
    }

    public static void QueueBevelledRectangle(
        TexturedBatch2D? texturedBatch,
        FlatBatch2D? flatBatch,
        Vector2 c1,
        Vector2 c2,
        float depth,
        float bevelSize,
        Color color,
        Color bevelColor,
        Color shadowColor,
        float ambientLight,
        float directionalLight,
        float textureScale
    )
    {
        var bevelWidth = MathUtils.Abs(bevelSize);
        var vector = c1;
        var innerTopLeft = c1 + new Vector2(bevelWidth);
        var innerBottomRight = c2 - new Vector2(bevelWidth);
        var outerBottomRight = c2;
        var shadowBottomRight = c2 + new Vector2(1.5f * bevelWidth);
        var x = vector.X;
        var x2 = innerTopLeft.X;
        var x3 = innerBottomRight.X;
        var x4 = outerBottomRight.X;
        var x5 = shadowBottomRight.X;
        var y = vector.Y;
        var y2 = innerTopLeft.Y;
        var y3 = innerBottomRight.Y;
        var y4 = outerBottomRight.Y;
        var y5 = shadowBottomRight.Y;
        var topBrightness = MathUtils.Saturate((bevelSize > 0f ? 1f : -0.75f) * directionalLight + ambientLight);
        var bottomBrightness = MathUtils.Saturate((bevelSize > 0f ? -0.75f : 1f) * directionalLight + ambientLight);
        var leftBrightness = MathUtils.Saturate((bevelSize > 0f ? -0.375f : 0.5f) * directionalLight + ambientLight);
        var rightBrightness = MathUtils.Saturate((bevelSize > 0f ? 0.5f : -0.375f) * directionalLight + ambientLight);
        var centerBrightness = MathUtils.Saturate(0f * directionalLight + ambientLight);
        var color2 = new Color(
            (byte)(leftBrightness * bevelColor.R),
            (byte)(leftBrightness * bevelColor.G),
            (byte)(leftBrightness * bevelColor.B),
            bevelColor.A
        );
        var color3 = new Color(
            (byte)(rightBrightness * bevelColor.R),
            (byte)(rightBrightness * bevelColor.G),
            (byte)(rightBrightness * bevelColor.B),
            bevelColor.A
        );
        var color4 = new Color(
            (byte)(topBrightness * bevelColor.R),
            (byte)(topBrightness * bevelColor.G),
            (byte)(topBrightness * bevelColor.B),
            bevelColor.A
        );
        var color5 = new Color(
            (byte)(bottomBrightness * bevelColor.R),
            (byte)(bottomBrightness * bevelColor.G),
            (byte)(bottomBrightness * bevelColor.B),
            bevelColor.A
        );
        var color6 = new Color(
            (byte)(centerBrightness * color.R),
            (byte)(centerBrightness * color.G),
            (byte)(centerBrightness * color.B),
            color.A
        );
        if (texturedBatch != null)
        {
            var uScale = textureScale / texturedBatch.Texture.Width;
            var vScale = textureScale / texturedBatch.Texture.Height;
            var originU = x * uScale;
            var originV = y * vScale;
            var x6 = originU;
            var x7 = (x2 - x) * uScale + originU;
            var x8 = (x3 - x) * uScale + originU;
            var x9 = (x4 - x) * uScale + originU;
            var y6 = originV;
            var y7 = (y2 - y) * vScale + originV;
            var y8 = (y3 - y) * vScale + originV;
            var y9 = (y4 - y) * vScale + originV;
            if (bevelColor.A > 0)
            {
                texturedBatch.QueueQuad(
                    new Vector2(x, y),
                    new Vector2(x2, y2),
                    new Vector2(x3, y2),
                    new Vector2(x4, y),
                    depth,
                    new Vector2(x6, y6),
                    new Vector2(x7, y7),
                    new Vector2(x8, y7),
                    new Vector2(x9, y6),
                    color4
                );
                texturedBatch.QueueQuad(
                    new Vector2(x3, y2),
                    new Vector2(x3, y3),
                    new Vector2(x4, y4),
                    new Vector2(x4, y),
                    depth,
                    new Vector2(x8, y7),
                    new Vector2(x8, y8),
                    new Vector2(x9, y9),
                    new Vector2(x9, y6),
                    color3
                );
                texturedBatch.QueueQuad(
                    new Vector2(x, y4),
                    new Vector2(x4, y4),
                    new Vector2(x3, y3),
                    new Vector2(x2, y3),
                    depth,
                    new Vector2(x6, y9),
                    new Vector2(x9, y9),
                    new Vector2(x8, y8),
                    new Vector2(x7, y8),
                    color5
                );
                texturedBatch.QueueQuad(
                    new Vector2(x, y),
                    new Vector2(x, y4),
                    new Vector2(x2, y3),
                    new Vector2(x2, y2),
                    depth,
                    new Vector2(x6, y6),
                    new Vector2(x6, y9),
                    new Vector2(x7, y8),
                    new Vector2(x7, y7),
                    color2
                );
            }

            if (color6.A > 0)
            {
                texturedBatch.QueueQuad(
                    new Vector2(x2, y2),
                    new Vector2(x3, y3),
                    depth,
                    new Vector2(x7, y7),
                    new Vector2(x8, y8),
                    color6
                );
            }
        }
        else if (flatBatch != null)
        {
            if (bevelColor.A > 0)
            {
                flatBatch.QueueQuad(
                    new Vector2(x, y),
                    new Vector2(x2, y2),
                    new Vector2(x3, y2),
                    new Vector2(x4, y),
                    depth,
                    color4
                );
                flatBatch.QueueQuad(
                    new Vector2(x3, y2),
                    new Vector2(x3, y3),
                    new Vector2(x4, y4),
                    new Vector2(x4, y),
                    depth,
                    color3
                );
                flatBatch.QueueQuad(
                    new Vector2(x, y4),
                    new Vector2(x4, y4),
                    new Vector2(x3, y3),
                    new Vector2(x2, y3),
                    depth,
                    color5
                );
                flatBatch.QueueQuad(
                    new Vector2(x, y),
                    new Vector2(x, y4),
                    new Vector2(x2, y3),
                    new Vector2(x2, y2),
                    depth,
                    color2
                );
            }

            if (color6.A > 0)
            {
                flatBatch.QueueQuad(new Vector2(x2, y2), new Vector2(x3, y3), depth, color6);
            }
        }

        if (bevelSize > 0f && flatBatch != null && shadowColor.A > 0)
        {
            var color7 = shadowColor;
            var color8 = new Color(0, 0, 0, 0);
            flatBatch.QueueTriangle(
                new Vector2(x, y4),
                new Vector2(x2, y5),
                new Vector2(x2, y4),
                depth,
                color8,
                color8,
                color7
            );
            flatBatch.QueueTriangle(
                new Vector2(x4, y),
                new Vector2(x4, y2),
                new Vector2(x5, y2),
                depth,
                color8,
                color7,
                color8
            );
            flatBatch.QueueTriangle(
                new Vector2(x4, y4),
                new Vector2(x4, y5),
                new Vector2(x5, y4),
                depth,
                color7,
                color8,
                color8
            );
            flatBatch.QueueQuad(
                new Vector2(x2, y4),
                new Vector2(x2, y5),
                new Vector2(x4, y5),
                new Vector2(x4, y4),
                depth,
                color7,
                color8,
                color8,
                color7
            );
            flatBatch.QueueQuad(
                new Vector2(x4, y2),
                new Vector2(x4, y4),
                new Vector2(x5, y4),
                new Vector2(x5, y2),
                depth,
                color7,
                color7,
                color8,
                color8
            );
        }
    }
}
