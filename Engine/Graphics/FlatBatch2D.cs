using Engine.Core;

namespace Engine.Graphics;

public sealed class FlatBatch2D : BaseFlatBatch
{
#if !ANDROID
    public FlatBatch2D()
    {
        DepthStencilState = DepthStencilState.None;
        RasterizerState = RasterizerState.CullNoneScissor;
        BlendState = BlendState.AlphaBlend;
    }
#endif

    public void QueueBatchTriangles(FlatBatch2D batch, Matrix? matrix = null, Color? color = null)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.AddRange(batch.TriangleVertices);
        var count2 = TriangleIndices.Count;
        var count3 = batch.TriangleIndices.Count;
        TriangleIndices.Count += count3;
        for (var i = 0; i < count3; i++)
        {
            TriangleIndices[i + count2] = (ushort)(batch.TriangleIndices[i] + count);
        }

        if (matrix.HasValue && matrix != Matrix.Identity)
        {
            TransformTriangles(matrix.Value, count);
        }

        if (color.HasValue && color != Color.White)
        {
            TransformTrianglesColors(color.Value, count);
        }
    }

    public void QueueBatchLines(FlatBatch2D batch, Matrix? matrix = null, Color? color = null)
    {
        var count = LineVertices.Count;
        LineVertices.AddRange(batch.LineVertices);
        var count2 = LineIndices.Count;
        var count3 = batch.LineIndices.Count;
        LineIndices.Count += count3;
        for (var i = 0; i < count3; i++)
        {
            LineIndices[i + count2] = (ushort)(batch.LineIndices[i] + count);
        }

        if (matrix.HasValue && matrix != Matrix.Identity)
        {
            TransformLines(matrix.Value, count);
        }

        if (color.HasValue && color != Color.White)
        {
            TransformLinesColors(color.Value, count);
        }
    }

    public void QueueBatch(FlatBatch2D batch, Matrix? matrix = null, Color? color = null)
    {
        QueueBatchLines(batch, matrix, color);
        QueueBatchTriangles(batch, matrix, color);
    }

    public void QueueLine(Vector2 p1, Vector2 p2, float depth, Color color)
    {
        var count = LineVertices.Count;
        LineVertices.Add(new VertexPositionColor(new Vector3(p1, depth), color));
        LineVertices.Add(new VertexPositionColor(new Vector3(p2, depth), color));
        LineIndices.Add((ushort)count);
        LineIndices.Add((ushort)(count + 1));
    }

    public void QueueLineStrip(IEnumerable<Vector2> points, float depth, Color color)
    {
        var count = LineVertices.Count;
        var num = 0;
        foreach (var point in points)
        {
            LineVertices.Add(new VertexPositionColor(new Vector3(point, depth), color));
            num++;
        }

        for (var i = 0; i < num - 1; i++)
        {
            LineIndices.Add((ushort)(count + i));
            LineIndices.Add((ushort)(count + i + 1));
        }
    }

    public void QueueRectangle(Vector2 corner1, Vector2 corner2, float depth, Color color)
    {
        var count = LineVertices.Count;
        LineVertices.Add(new VertexPositionColor(new Vector3(corner1.X, corner1.Y, depth), color));
        LineVertices.Add(new VertexPositionColor(new Vector3(corner1.X, corner2.Y, depth), color));
        LineVertices.Add(new VertexPositionColor(new Vector3(corner2.X, corner2.Y, depth), color));
        LineVertices.Add(new VertexPositionColor(new Vector3(corner2.X, corner1.Y, depth), color));
        LineIndices.Add((ushort)count);
        LineIndices.Add((ushort)(count + 1));
        LineIndices.Add((ushort)(count + 1));
        LineIndices.Add((ushort)(count + 2));
        LineIndices.Add((ushort)(count + 2));
        LineIndices.Add((ushort)(count + 3));
        LineIndices.Add((ushort)(count + 3));
        LineIndices.Add((ushort)count);
    }

    public void QueueEllipse(Vector2 center, Vector2 radius, float depth, Color color, int sides = 32,
        float startAngle = 0f, float endAngle = (float)Math.PI * 2f)
    {
        var p = Vector2.Zero;
        for (var i = 0; i <= sides; i++)
        {
            var x = MathUtils.Lerp(startAngle, endAngle, i / (float)sides);
            var vector = center + radius * new Vector2(MathUtils.Sin(x), 0f - MathUtils.Cos(x));
            if (i > 0)
            {
                QueueLine(p, vector, depth, color);
            }

            p = vector;
        }
    }

    public void QueueDisc(Vector2 center, Vector2 radius, float depth, Color color, int sides = 32,
        float startAngle = 0f, float endAngle = (float)Math.PI * 2f)
    {
        var p = Vector2.Zero;
        for (var i = 0; i <= sides; i++)
        {
            var x = MathUtils.Lerp(startAngle, endAngle, i / (float)sides);
            var vector = center + radius * new Vector2(MathUtils.Sin(x), 0f - MathUtils.Cos(x));
            if (i > 0)
            {
                QueueTriangle(p, vector, center, depth, color);
            }

            p = vector;
        }
    }

    public void QueueDisc(Vector2 center, Vector2 outerRadius, Vector2 innerRadius, float depth, Color outerColor,
        Color innerColor, int sides = 32, float startAngle = 0f, float endAngle = (float)Math.PI * 2f)
    {
        var p = Vector2.Zero;
        var p2 = Vector2.Zero;
        for (var i = 0; i <= sides; i++)
        {
            var x = MathUtils.Lerp(startAngle, endAngle, i / (float)sides);
            var v = new Vector2(MathUtils.Sin(x), 0f - MathUtils.Cos(x));
            var vector = center + outerRadius * v;
            var vector2 = center + innerRadius * v;
            if (i > 0)
            {
                QueueTriangle(p, vector, p2, depth, outerColor, outerColor, innerColor);
                QueueTriangle(vector, vector2, p2, depth, outerColor, innerColor, innerColor);
            }

            p = vector;
            p2 = vector2;
        }
    }

    public void QueueTriangle(Vector2 p1, Vector2 p2, Vector2 p3, float depth, Color color)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.Add(new VertexPositionColor(new Vector3(p1.X, p1.Y, depth), color));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(p2.X, p2.Y, depth), color));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(p3.X, p3.Y, depth), color));
        TriangleIndices.Add((ushort)count);
        TriangleIndices.Add((ushort)(count + 1));
        TriangleIndices.Add((ushort)(count + 2));
    }

    public void QueueTriangle(Vector2 p1, Vector2 p2, Vector2 p3, float depth, Color color1, Color color2, Color color3)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.Add(new VertexPositionColor(new Vector3(p1.X, p1.Y, depth), color1));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(p2.X, p2.Y, depth), color2));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(p3.X, p3.Y, depth), color3));
        TriangleIndices.Add((ushort)count);
        TriangleIndices.Add((ushort)(count + 1));
        TriangleIndices.Add((ushort)(count + 2));
    }

    public void QueueQuad(Vector2 corner1, Vector2 corner2, float depth, Color color)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.Add(new VertexPositionColor(new Vector3(corner1.X, corner1.Y, depth), color));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(corner1.X, corner2.Y, depth), color));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(corner2.X, corner2.Y, depth), color));
        TriangleVertices.Add(new VertexPositionColor(new Vector3(corner2.X, corner1.Y, depth), color));
        TriangleIndices.Add((ushort)count);
        TriangleIndices.Add((ushort)(count + 1));
        TriangleIndices.Add((ushort)(count + 2));
        TriangleIndices.Add((ushort)(count + 2));
        TriangleIndices.Add((ushort)(count + 3));
        TriangleIndices.Add((ushort)count);
    }

    public void QueueQuad(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float depth, Color color)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.Count += 4;
        TriangleVertices.Array[count] = new VertexPositionColor(new Vector3(p1.X, p1.Y, depth), color);
        TriangleVertices.Array[count + 1] = new VertexPositionColor(new Vector3(p2.X, p2.Y, depth), color);
        TriangleVertices.Array[count + 2] = new VertexPositionColor(new Vector3(p3.X, p3.Y, depth), color);
        TriangleVertices.Array[count + 3] = new VertexPositionColor(new Vector3(p4.X, p4.Y, depth), color);
        var count2 = TriangleIndices.Count;
        TriangleIndices.Count += 6;
        TriangleIndices.Array[count2] = (ushort)count;
        TriangleIndices.Array[count2 + 1] = (ushort)(count + 1);
        TriangleIndices.Array[count2 + 2] = (ushort)(count + 2);
        TriangleIndices.Array[count2 + 3] = (ushort)(count + 2);
        TriangleIndices.Array[count2 + 4] = (ushort)(count + 3);
        TriangleIndices.Array[count2 + 5] = (ushort)count;
    }

    public void QueueQuad(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float depth, Color color1, Color color2,
        Color color3, Color color4)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.Count += 4;
        TriangleVertices.Array[count] = new VertexPositionColor(new Vector3(p1.X, p1.Y, depth), color1);
        TriangleVertices.Array[count + 1] = new VertexPositionColor(new Vector3(p2.X, p2.Y, depth), color2);
        TriangleVertices.Array[count + 2] = new VertexPositionColor(new Vector3(p3.X, p3.Y, depth), color3);
        TriangleVertices.Array[count + 3] = new VertexPositionColor(new Vector3(p4.X, p4.Y, depth), color4);
        var count2 = TriangleIndices.Count;
        TriangleIndices.Count += 6;
        TriangleIndices.Array[count2] = (ushort)count;
        TriangleIndices.Array[count2 + 1] = (ushort)(count + 1);
        TriangleIndices.Array[count2 + 2] = (ushort)(count + 2);
        TriangleIndices.Array[count2 + 3] = (ushort)(count + 2);
        TriangleIndices.Array[count2 + 4] = (ushort)(count + 3);
        TriangleIndices.Array[count2 + 5] = (ushort)count;
    }

    public new void TransformLines(Matrix matrix, int start = 0, int end = -1)
    {
        var array = LineVertices.Array;
        if (end < 0)
        {
            end = LineVertices.Count;
        }

        for (var i = start; i < end; i++)
        {
            var v = array[i].Position.XY;
            Vector2.Transform(ref v, ref matrix, out v);
            array[i].Position.X = v.X;
            array[i].Position.Y = v.Y;
        }
    }

    public new void TransformTriangles(Matrix matrix, int start = 0, int end = -1)
    {
        var array = TriangleVertices.Array;
        if (end < 0)
        {
            end = TriangleVertices.Count;
        }

        for (var i = start; i < end; i++)
        {
            var v = array[i].Position.XY;
            Vector2.Transform(ref v, ref matrix, out v);
            array[i].Position.X = v.X;
            array[i].Position.Y = v.Y;
        }
    }

    public void Flush(bool clearAfterFlush = true)
    {
        Flush(PrimitivesRenderer2D.ViewportMatrix(), clearAfterFlush);
    }
}
