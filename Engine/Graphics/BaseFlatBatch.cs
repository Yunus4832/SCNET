using Engine.Core;

namespace Engine.Graphics;

public abstract class BaseFlatBatch : BaseBatch
{
    internal static UnlitShader shader = new(true, false, false, false);

    public readonly DynamicArray<int> LineIndices = [];

    public readonly DynamicArray<VertexPositionColor> LineVertices = [];

    public readonly DynamicArray<int> TriangleIndices = [];

    public readonly DynamicArray<VertexPositionColor> TriangleVertices = [];

    internal BaseFlatBatch()
    {
    }

    public override bool IsEmpty()
    {
        if (LineIndices.Count == 0)
        {
            return TriangleIndices.Count == 0;
        }

        return false;
    }

    public override void Clear()
    {
        LineVertices.Clear();
        LineIndices.Clear();
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
        FlushWithDeviceState(matrix, color, clearAfterFlush);
    }

    public void FlushWithCurrentState(Matrix matrix, bool clearAfterFlush = true)
    {
        if (IsEmpty())
        {
            return;
        }

        shader.Transforms.World[0] = matrix;
        FlushWithDeviceState(shader, clearAfterFlush);
    }

    public void FlushWithCurrentStateAndShader(Shader inputShader, bool clearAfterFlush = true)
    {
        if (TriangleIndices.Count > 0)
        {
            var num = 0;
            var num2 = TriangleIndices.Count;
            while (num2 > 0)
            {
                var num3 = MathUtils.Min(num2, 196605);
                Display.DrawUserIndexed(PrimitiveType.TriangleList, inputShader, VertexPositionColor.VertexDeclaration,
                    TriangleVertices.Array, 0, TriangleVertices.Count, TriangleIndices.Array, num, num3);
                num += num3;
                num2 -= num3;
            }
        }

        if (LineIndices.Count > 0)
        {
            var num4 = 0;
            var num5 = LineIndices.Count;
            while (num5 > 0)
            {
                var num6 = MathUtils.Min(num5, 131070);
                Display.DrawUserIndexed(PrimitiveType.LineList, inputShader, VertexPositionColor.VertexDeclaration,
                    LineVertices.Array, 0, LineVertices.Count, LineIndices.Array, num4, num6);
                num4 += num6;
                num5 -= num6;
            }
        }

        if (clearAfterFlush)
        {
            Clear();
        }
    }

    public void FlushWithDeviceState(Matrix matrix, Vector4 color, bool clearAfterFlush = true)
    {
        if (IsEmpty())
        {
            return;
        }

        shader.Transforms.World[0] = matrix;
        shader.Color = color;
        FlushWithDeviceState(shader, clearAfterFlush);
    }

    public void FlushWithDeviceState(Shader inputShader, bool clearAfterFlush = true)
    {
        if (TriangleIndices.Count > 0)
        {
            var num = 0;
            var num2 = TriangleIndices.Count;
            while (num2 > 0)
            {
                var num3 = Math.Min(num2, 196605);
                Display.DrawUserIndexed(PrimitiveType.TriangleList, inputShader, VertexPositionColor.VertexDeclaration,
                    TriangleVertices.Array, 0, TriangleVertices.Count, TriangleIndices.Array, num, num3);
                num += num3;
                num2 -= num3;
            }
        }

        if (LineIndices.Count > 0)
        {
            var num4 = 0;
            var num5 = LineIndices.Count;
            while (num5 > 0)
            {
                var num6 = Math.Min(num5, 131070);
                Display.DrawUserIndexed(PrimitiveType.LineList, inputShader, VertexPositionColor.VertexDeclaration,
                    LineVertices.Array, 0, LineVertices.Count, LineIndices.Array, num4, num6);
                num4 += num6;
                num5 -= num6;
            }
        }

        if (clearAfterFlush)
        {
            Clear();
        }
    }

    public void TransformLines(Matrix matrix, int start = 0, int end = -1)
    {
        var array = LineVertices.Array;
        if (end < 0)
        {
            end = LineVertices.Count;
        }

        for (var i = start; i < end; i++)
        {
            Vector3.Transform(ref array[i].Position, ref matrix, out array[i].Position);
        }
    }

    public void TransformLinesColors(Color color, int start = 0, int end = -1)
    {
        var array = LineVertices.Array;
        if (end < 0)
        {
            end = LineVertices.Count;
        }

        for (var i = start; i < end; i++)
        {
            array[i].Color *= color;
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
}
