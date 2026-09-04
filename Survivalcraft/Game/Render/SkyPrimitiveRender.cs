using Engine.Graphics;

namespace Game;

public class SkyPrimitiveRender
{
    public Camera? Camera;

    public SkyShader? Shader;

    public SkyShader? ShaderAlphaTest;

    public void TexturedFlushWithCurrentStateAndShader(
        BaseTexturedBatch baseTexturedBatch,
        Shader shader,
        bool clearAfterFlush = true
    )
    {
        var num = 0;
        var num2 = baseTexturedBatch.TriangleIndices.Count;
        while (num2 > 0)
        {
            var num3 = MathUtils.Min(num2, 196605);
            Display.DrawUserIndexed(PrimitiveType.TriangleList, shader, VertexPositionColorTexture.VertexDeclaration,
                baseTexturedBatch.TriangleVertices.Array, 0, baseTexturedBatch.TriangleVertices.Count,
                baseTexturedBatch.TriangleIndices.Array, num, num3);
            num += num3;
            num2 -= num3;
        }

        if (clearAfterFlush)
        {
            baseTexturedBatch.Clear();
        }
    }

    public void Flush(
        PrimitivesRenderer3D primitiveRend,
        Matrix matrix,
        bool clearAfterFlush = true,
        int maxLayer = 2147483647
    )
    {
        if (ShaderAlphaTest is null || Shader is null || Camera is null)
        {
            return;
        }

        if (primitiveRend.SortNeeded)
        {
            primitiveRend.SortNeeded = false;
            primitiveRend.AllBatches.Sort(delegate (BaseBatch b1, BaseBatch b2)
            {
                if (b1.Layer < b2.Layer)
                {
                    return -1;
                }

                if (b1.Layer <= b2.Layer)
                {
                    return 0;
                }

                return 1;
            });
        }

        foreach (var baseBatch in primitiveRend.AllBatches)
        {
            if (baseBatch.Layer > maxLayer)
            {
                break;
            }

            if (!baseBatch.IsEmpty() && baseBatch is TexturedBatch3D)
            {
                var baseTexturedBatch = (BaseTexturedBatch)baseBatch;
                Display.DepthStencilState = baseTexturedBatch.DepthStencilState;
                Display.RasterizerState = baseTexturedBatch.RasterizerState;
                Display.BlendState = baseTexturedBatch.BlendState;
                if (baseTexturedBatch.UseAlphaTest)
                {
                    ShaderAlphaTest.Texture = baseTexturedBatch.Texture;
                    ShaderAlphaTest.SamplerState = baseTexturedBatch.SamplerState;
                    ShaderAlphaTest.Transforms.World[0] = matrix;
                    ShaderAlphaTest.AlphaThreshold = 0f;
                    baseTexturedBatch.FlushWithDeviceState(ShaderAlphaTest, clearAfterFlush); //new
                }
                else
                {
                    Shader.Texture = baseTexturedBatch.Texture;
                    Shader.SamplerState = baseTexturedBatch.SamplerState;
                    Shader.Transforms.World[0] = matrix;
                    baseTexturedBatch.FlushWithDeviceState(Shader, clearAfterFlush); //new
                }
            }
            else
            {
                baseBatch.Flush(matrix, Vector4.One);
            }
        }
    }
}
