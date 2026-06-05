using Engine.Core;
using Engine.Media;

namespace Engine.Graphics;

public abstract class BaseFontBatch : BaseBatch
{
    public static UnlitShader? Shader;

    public readonly DynamicArray<int> TriangleIndices = [];

    public readonly DynamicArray<VertexPositionColorTexture> TriangleVertices = [];

    public BitmapFont Font { get; set; } = BitmapFont.DebugFont;

    public SamplerState SamplerState { get; set; } = SamplerState.LinearClamp;

    public override bool IsEmpty()
    {
        return TriangleIndices.Count == 0;
    }

    public override void Clear()
    {
        TriangleVertices.Clear();
        TriangleIndices.Clear();
    }

    public void Flush(Matrix matrix, bool clearAfterFlush = true)
    {
        Flush(matrix, Vector4.One, clearAfterFlush);
    }

    public override void Flush(Matrix matrix, Vector4 color, bool clearAfterFlush = true)
    {
        Display.DepthStencilState = DepthStencilState;
        Display.RasterizerState = RasterizerState;
        Display.BlendState = BlendState;
        FlushWithDeviceState(Font, SamplerState, matrix, color, clearAfterFlush);
    }

    public void FlushWithDeviceState(BitmapFont font, SamplerState samplerState, Matrix matrix, Vector4 color,
        bool clearAfterFlush = true)
    {
        if (font.Texture is null)
        {
            throw new ArgumentNullException(nameof(font.Texture));
        }

        Shader ??= new UnlitShader(true, true, false, false);
        Shader.Texture = font.Texture;
        Shader.SamplerState = samplerState;
        Shader.Transforms.World[0] = matrix;
        Shader.Color = color;
        FlushWithDeviceState(Shader, clearAfterFlush);
    }

    public void FlushWithDeviceState(Shader shader, bool clearAfterFlush = true)
    {
        if (TriangleIndices.Count > 0)
        {
            var num = 0;
            var num2 = TriangleIndices.Count;
            while (num2 > 0)
            {
                var num3 = MathUtils.Min(num2, 196605);
                Display.DrawUserIndexed(PrimitiveType.TriangleList, shader,
                    VertexPositionColorTexture.VertexDeclaration, TriangleVertices.Array, 0, TriangleVertices.Count,
                    TriangleIndices.Array, num, num3);
                num += num3;
                num2 -= num3;
            }
        }

        if (clearAfterFlush)
        {
            Clear();
        }
    }

    public void FlushWithCurrentStateAndShader(Shader shader, bool clearAfterFlush = true)
    {
        if (TriangleIndices.Count > 0)
        {
            var num = 0;
            var num2 = TriangleIndices.Count;
            while (num2 > 0)
            {
                var num3 = Math.Min(num2, 196605);
                Display.DrawUserIndexed(PrimitiveType.TriangleList, shader,
                    VertexPositionColorTexture.VertexDeclaration, TriangleVertices.Array, 0, TriangleVertices.Count,
                    TriangleIndices.Array, num, num3);
                num += num3;
                num2 -= num3;
            }
        }

        if (clearAfterFlush)
        {
            Clear();
        }
    }

    public void TransformTriangles(Matrix matrix, int start = 0, int end = -1)
    {
        var array = TriangleVertices.Array;
        if (end < 0)
        {
            end = TriangleVertices.Count;
        }

        for (var i = start; i < end; i++)
        {
            Vector3.Transform(ref array[i].Position, ref matrix, out array[i].Position);
        }
    }

    public void TransformTrianglesColors(Color color, int start = 0, int end = -1)
    {
        var array = TriangleVertices.Array;
        if (end < 0)
        {
            end = TriangleVertices.Count;
        }

        for (var i = start; i < end; i++)
        {
            array[i].Color *= color;
        }
    }

    public Vector2 CalculateTextOffset(string text, int start, int count, TextAnchor anchor, Vector2 scale,
        Vector2 spacing)
    {
        var zero = Vector2.Zero;
        if (anchor == 0)
        {
            return zero;
        }

        var vector = Font.MeasureText(text, start, count, scale, spacing);
        if ((anchor & TextAnchor.HorizontalCenter) != 0)
        {
            zero.X = (0f - vector.X) / 2f;
        }
        else if ((anchor & TextAnchor.Right) != 0)
        {
            zero.X = 0f - vector.X;
        }

        if ((anchor & TextAnchor.VerticalCenter) != 0)
        {
            zero.Y = (0f - vector.Y) / 2f;
        }
        else if ((anchor & TextAnchor.Bottom) != 0)
        {
            zero.Y = 0f - vector.Y;
        }

        return zero;
    }
}
