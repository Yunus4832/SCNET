#if DESKTOP
using Engine.Media;
#endif
using Engine.Core;

namespace Engine.Graphics;

public class FontBatch2D : BaseFontBatch
{
    public void QueueText(string text, Vector2 position, float depth, Color color,
        TextAnchor anchor = TextAnchor.Default)
    {
        QueueText(text, position, depth, color, anchor, Vector2.One, Vector2.Zero);
    }

    public void QueueText(string text, Vector2 position, float depth, Color color, TextAnchor anchor, Vector2 scale,
        Vector2 spacing, float angle = 0f)
    {
        Vector2 v;
        Vector2 v2;
        Vector2 vector3;
        if (angle != 0f)
        {
            var vector = new Vector2(MathUtils.Cos(angle), MathUtils.Sin(angle));
            v = vector;
            v2 = new Vector2(0f - vector.Y, vector.X);
            var vector2 = CalculateTextOffset(text, 0, text.Length, anchor, scale, spacing);
            var v3 = v * vector2.X + v2 * vector2.Y;
            v *= scale.X * Font.Scale;
            v2 *= scale.Y * Font.Scale;
            vector3 = position + v3;
        }
        else
        {
            v = new Vector2(scale.X * Font.Scale, 0f);
            v2 = new Vector2(0f, scale.Y * Font.Scale);
            vector3 = position + CalculateTextOffset(text, 0, text.Length, anchor, scale, spacing);
        }

        spacing += Font.Spacing;
        vector3 += 0.5f * (v * spacing.X + v2 * spacing.Y);
        if ((anchor & TextAnchor.DisableSnapToPixels) == 0)
        {
            vector3 = Vector2.Round(vector3);
        }

        var v4 = vector3;
        var num = 0;
        foreach (var c in text)
        {
            switch (c)
            {
                case '\n':
                    num++;
                    v4 = vector3 + num * (Font.GlyphHeight + spacing.Y) * v2;
                    continue;
                case '\r':
                    continue;
            }

            var glyph = Font.GetGlyph(c);
            if (!glyph.IsBlank)
            {
                if (Font.Texture == null)
                {
                    throw new ArgumentNullException(nameof(Font.Texture));
                }

                var v5 = v * (glyph.TexCoord2.X - glyph.TexCoord1.X) * Font.Texture.Width;
                var v6 = v2 * (glyph.TexCoord2.Y - glyph.TexCoord1.Y) * Font.Texture.Height;
                var v7 = v * glyph.Offset.X + v2 * glyph.Offset.Y;
                var v8 = v4 + v7;
                var vector4 = v8 + v5;
                var vector5 = v8 + v6;
                var vector6 = v8 + v5 + v6;
                var count = TriangleVertices.Count;
                TriangleVertices.Count += 4;
                TriangleVertices.Array[count] = new VertexPositionColorTexture(new Vector3(v8.X, v8.Y, depth), color,
                    new Vector2(glyph.TexCoord1.X, glyph.TexCoord1.Y));
                TriangleVertices.Array[count + 1] = new VertexPositionColorTexture(
                    new Vector3(vector4.X, vector4.Y, depth), color, new Vector2(glyph.TexCoord2.X, glyph.TexCoord1.Y));
                TriangleVertices.Array[count + 2] = new VertexPositionColorTexture(
                    new Vector3(vector6.X, vector6.Y, depth), color, new Vector2(glyph.TexCoord2.X, glyph.TexCoord2.Y));
                TriangleVertices.Array[count + 3] = new VertexPositionColorTexture(
                    new Vector3(vector5.X, vector5.Y, depth), color, new Vector2(glyph.TexCoord1.X, glyph.TexCoord2.Y));
                var count2 = TriangleIndices.Count;
                TriangleIndices.Count += 6;
                TriangleIndices.Array[count2] = (ushort)count;
                TriangleIndices.Array[count2 + 1] = (ushort)(count + 1);
                TriangleIndices.Array[count2 + 2] = (ushort)(count + 2);
                TriangleIndices.Array[count2 + 3] = (ushort)(count + 2);
                TriangleIndices.Array[count2 + 4] = (ushort)(count + 3);
                TriangleIndices.Array[count2 + 5] = (ushort)count;
            }

            v4 += v * (glyph.Width + spacing.X);
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
#if DESKTOP
    public FontBatch2D()
    {
        Font = BitmapFont.DebugFont;
        DepthStencilState = DepthStencilState.None;
        RasterizerState = RasterizerState.CullNoneScissor;
        BlendState = BlendState.AlphaBlend;
        SamplerState = SamplerState.LinearClamp;
    }

    public void QueueBatch(FontBatch2D batch, Matrix? matrix = null, Color? color = null)
    {
        var count = TriangleVertices.Count;
        TriangleVertices.AddRange(batch.TriangleVertices);
        for (var i = 0; i < batch.TriangleIndices.Count; i++)
        {
            TriangleIndices.Add((ushort)(batch.TriangleIndices[i] + count));
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
#endif
}
