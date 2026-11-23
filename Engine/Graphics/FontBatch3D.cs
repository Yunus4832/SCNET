using Engine.Core.Mathematics;

namespace Engine.Graphics;

public class FontBatch3D : BaseFontBatch
{
    public FontBatch3D(BitmapFont font) : base(font)
    {
        DepthStencilState = DepthStencilState.Default;
        RasterizerState = RasterizerState.CullNoneScissor;
        BlendState = BlendState.AlphaBlend;
        SamplerState = SamplerState.LinearClamp;
    }

    public void QueueText(
        string text,
        Vector3 position,
        Vector3 right,
        Vector3 down,
        Color color,
        TextAnchor anchor = TextAnchor.Default
    )
    {
        QueueText(text, position, right, down, color, anchor, Vector2.Zero);
    }

    public void QueueText(
        string text,
        Vector3 position,
        Vector3 right,
        Vector3 down,
        Color color,
        TextAnchor anchor,
        Vector2 spacing
    )
    {
        var scale = new Vector2(right.Length(), down.Length());
        var vector = CalculateTextOffset(text, 0, text.Length, anchor, scale, spacing);
        var textOrigin = position + vector.X * Vector3.Normalize(right) + vector.Y * Vector3.Normalize(down);
        var v = textOrigin;
        right *= Font.Scale;
        down *= Font.Scale;
        var lineIndex = 0;
        foreach (var c in text)
        {
            switch (c)
            {
                case '\n':
                    lineIndex++;
                    v = textOrigin + lineIndex * (Font.GlyphHeight + Font.Spacing.Y + spacing.Y) * down;
                    continue;
                case '\r':
                    continue;
            }

            var glyph = Font.GetGlyph(c);
            if (!glyph.IsBlank)
            {
                if (Font.Texture is null)
                {
                    throw new ArgumentNullException(nameof(Font.Texture));
                }

                var v2 = right * (glyph.TexCoord2.X - glyph.TexCoord1.X) * Font.Texture.Width;
                var v3 = down * (glyph.TexCoord2.Y - glyph.TexCoord1.Y) * Font.Texture.Height;
                var v4 = right * glyph.Offset.X + down * glyph.Offset.Y;
                var v5 = v + v4;
                var topRight = v5 + v2;
                var bottomLeft = v5 + v3;
                var bottomRight = v5 + v2 + v3;
                var count = TriangleVertices.Count;
                TriangleVertices.Count += 4;
                TriangleVertices.Array[count] = new VertexPositionColorTexture(
                    new Vector3(v5.X, v5.Y, v5.Z),
                    color,
                    new Vector2(glyph.TexCoord1.X, glyph.TexCoord1.Y)
                );
                TriangleVertices.Array[count + 1] = new VertexPositionColorTexture(
                    new Vector3(topRight.X, topRight.Y, topRight.Z),
                    color,
                    new Vector2(glyph.TexCoord2.X, glyph.TexCoord1.Y)
                );
                TriangleVertices.Array[count + 2] = new VertexPositionColorTexture(
                    new Vector3(bottomRight.X, bottomRight.Y, bottomRight.Z),
                    color,
                    new Vector2(glyph.TexCoord2.X, glyph.TexCoord2.Y)
                );
                TriangleVertices.Array[count + 3] = new VertexPositionColorTexture(
                    new Vector3(bottomLeft.X, bottomLeft.Y, bottomLeft.Z),
                    color,
                    new Vector2(glyph.TexCoord1.X, glyph.TexCoord2.Y)
                );
                var count2 = TriangleIndices.Count;
                TriangleIndices.Count += 6;
                TriangleIndices.Array[count2] = count;
                TriangleIndices.Array[count2 + 1] = count + 1;
                TriangleIndices.Array[count2 + 2] = count + 2;
                TriangleIndices.Array[count2 + 3] = count + 2;
                TriangleIndices.Array[count2 + 4] = count + 3;
                TriangleIndices.Array[count2 + 5] = count;
            }

            v += right * (glyph.Width + Font.Spacing.X + spacing.X);
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
            Vector3.Transform(ref array[i].Position, ref matrix, out array[i].Position);
        }
    }
}
