using Engine.Graphics;

namespace Game.Widgets;

public class ValueBarWidget : Widget
{
    private float _flashCount;

    private LayoutDirection _layoutDirection;

    public override bool IsHitTestVisible { get; set; } = false;

    public float Value
    {
        get;
        set => field = MathUtils.Saturate(value);
    }

    public int BarsCount
    {
        get;
        set => field = MathUtils.Clamp(value, 1, 1000);
    } = 8;

    public bool FlipDirection { get; set; }

    public Vector2 BarSize { get; set; } = new(24f);

    public float Spacing { get; set; }

    public Color LitBarColor { get; set; } = new(16, 140, 0);

    public Color LitBarColor2 { get; set; } = Color.Transparent;

    public Color UnlitBarColor { get; set; } = new(48, 48, 48);

    public bool BarBlending { get; set; } = true;

    public bool HalfBars { get; set; }

    public Subtexture? BarSubtexture { get; set; }

    public bool TextureLinearFilter { get; set; } = true;

    public LayoutDirection LayoutDirection
    {
        get => _layoutDirection;
        set => _layoutDirection = value;
    }


    public void Flash(int count)
    {
        _flashCount = MathUtils.Max(_flashCount, count);
    }

    public override void Draw(DrawContext dc)
    {
        BaseBatch baseBatch = BarSubtexture == null
            ? dc.PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None)
            : dc.PrimitivesRenderer2D.TexturedBatch(BarSubtexture.Texture, false, 0, DepthStencilState.None,
                null, null, TextureLinearFilter ? SamplerState.LinearClamp : SamplerState.PointClamp);
        var start = 0;
        int num;
        if (baseBatch is TexturedBatch2D texturedBatch2D)
        {
            num = texturedBatch2D.TriangleVertices.Count;
        }
        else
        {
            start = ((FlatBatch2D)baseBatch).LineVertices.Count;
            num = ((FlatBatch2D)baseBatch).TriangleVertices.Count;
        }

        var zero = Vector2.Zero;
        if (_layoutDirection == LayoutDirection.Horizontal)
        {
            zero.X += Spacing / 2f;
        }
        else
        {
            zero.Y += Spacing / 2f;
        }

        var num2 = HalfBars ? 1 : 2;
        for (var i = 0; i < 2 * BarsCount; i += num2)
        {
            var flag = i % 2 == 0;
            var num3 = 0.5f * i;
            var num4 = !FlipDirection
                ? MathUtils.Clamp((Value - num3 / BarsCount) * BarsCount, 0f, 1f)
                : MathUtils.Clamp((Value - (BarsCount - num3 - 1f) / BarsCount) * BarsCount, 0f, 1f);
            if (!BarBlending)
            {
                num4 = MathUtils.Ceiling(num4);
            }

            var s = _flashCount > 0f ? 1f - MathUtils.Abs(MathUtils.Sin(_flashCount * (float)Math.PI)) : 1f;
            var c = LitBarColor;
            if (LitBarColor2 != Color.Transparent && BarsCount > 1)
            {
                c = Color.Lerp(LitBarColor, LitBarColor2, num3 / (BarsCount - 1));
            }

            var color = Color.Lerp(UnlitBarColor, c, num4) * s * GlobalColorTransform;
            if (HalfBars)
            {
                if (flag)
                {
                    var zero2 = Vector2.Zero;
                    var v = _layoutDirection == LayoutDirection.Horizontal
                        ? new Vector2(0.5f, 1f)
                        : new Vector2(1f, 0.5f);
                    if (baseBatch is TexturedBatch2D batch2D)
                    {
                        if (BarSubtexture != null)
                        {
                            var topLeft = BarSubtexture.TopLeft;
                            var texCoord =
                                new Vector2(MathUtils.Lerp(BarSubtexture.TopLeft.X, BarSubtexture.BottomRight.X, v.X),
                                    MathUtils.Lerp(BarSubtexture.TopLeft.Y, BarSubtexture.BottomRight.Y, v.Y));
                            batch2D.QueueQuad(zero + zero2 * BarSize, zero + v * BarSize, 0f, topLeft,
                                texCoord, color);
                        }
                    }
                    else
                    {
                        ((FlatBatch2D)baseBatch).QueueQuad(zero + zero2 * BarSize, zero + v * BarSize, 0f, color);
                    }
                }
                else
                {
                    var v2 = _layoutDirection == LayoutDirection.Horizontal
                        ? new Vector2(0.5f, 0f)
                        : new Vector2(0f, 0.5f);
                    var one = Vector2.One;
                    if (baseBatch is TexturedBatch2D batch2D)
                    {
                        if (BarSubtexture != null)
                        {
                            var texCoord2 =
                                new Vector2(MathUtils.Lerp(BarSubtexture.TopLeft.X, BarSubtexture.BottomRight.X, v2.X),
                                    MathUtils.Lerp(BarSubtexture.TopLeft.Y, BarSubtexture.BottomRight.Y, v2.Y));
                            var bottomRight = BarSubtexture.BottomRight;
                            batch2D.QueueQuad(zero + v2 * BarSize, zero + one * BarSize, 0f, texCoord2,
                                bottomRight, color);
                        }
                    }
                    else
                    {
                        ((FlatBatch2D)baseBatch).QueueQuad(zero + v2 * BarSize, zero + one * BarSize, 0f, color);
                    }
                }
            }
            else
            {
                var zero3 = Vector2.Zero;
                var one2 = Vector2.One;
                if (baseBatch is TexturedBatch2D batch2D)
                {
                    if (BarSubtexture != null)
                    {
                        var topLeft2 = BarSubtexture.TopLeft;
                        var bottomRight2 = BarSubtexture.BottomRight;
                        batch2D.QueueQuad(zero + zero3 * BarSize, zero + one2 * BarSize, 0f, topLeft2,
                            bottomRight2, color);
                    }
                }
                else
                {
                    ((FlatBatch2D)baseBatch).QueueQuad(zero + zero3 * BarSize, zero + one2 * BarSize, 0f, color);
                    ((FlatBatch2D)baseBatch).QueueRectangle(zero + zero3 * BarSize, zero + one2 * BarSize, 0f,
                        Color.MultiplyColorOnly(color, 0.75f));
                }
            }

            if (flag && HalfBars)
            {
                continue;
            }

            if (_layoutDirection == LayoutDirection.Horizontal)
            {
                zero.X += BarSize.X + Spacing;
            }
            else
            {
                zero.Y += BarSize.Y + Spacing;
            }
        }

        if (baseBatch is TexturedBatch2D textureBatch)
        {
            textureBatch.TransformTriangles(GlobalTransform, num);
        }
        else
        {
            ((FlatBatch2D)baseBatch).TransformLines(GlobalTransform, start);
            ((FlatBatch2D)baseBatch).TransformTriangles(GlobalTransform, num);
        }

        _flashCount = MathUtils.Max(_flashCount - 4f * Time.FrameDuration, 0f);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        DesiredSize = _layoutDirection == LayoutDirection.Horizontal
            ? new Vector2((BarSize.X + Spacing) * BarsCount, BarSize.Y)
            : new Vector2(BarSize.X, (BarSize.Y + Spacing) * BarsCount);
    }
}
