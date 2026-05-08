using Engine.Core;

namespace Engine.Graphics;

public abstract class BaseTexturedBatch : BaseBatch
{
    public static readonly UnlitShader Shader = new(true, true, false, false);

    public static readonly UnlitShader ShaderAlphaTest = new(true, true, false, true);

    public readonly DynamicArray<int> TriangleIndices = [];

    public readonly DynamicArray<VertexPositionColorTexture> TriangleVertices = [];

    public Texture2D Texture { get; set; } = null!;

    public bool UseAlphaTest { get; set; }

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
        FlushWithDeviceState(UseAlphaTest, Texture, SamplerState, matrix, color, clearAfterFlush);
    }

    public void FlushWithCurrentState(bool useAlphaTest, Texture2D texture, SamplerState samplerState, Matrix matrix,
        bool clearAfterFlush = true)
    {
        if (useAlphaTest)
        {
            ShaderAlphaTest.Texture = texture;
            ShaderAlphaTest.SamplerState = samplerState;
            ShaderAlphaTest.Transforms.World[0] = matrix;
            ShaderAlphaTest.AlphaThreshold = 0f;
            FlushWithCurrentStateAndShader(ShaderAlphaTest, clearAfterFlush);
        }
        else
        {
            Shader.Texture = texture;
            Shader.SamplerState = samplerState;
            Shader.Transforms.World[0] = matrix;
            FlushWithCurrentStateAndShader(Shader, clearAfterFlush);
        }
    }

    public void FlushWithCurrentStateAndShader(Shader shader, bool clearAfterFlush = true)
    {
        var num = 0;
        var num2 = TriangleIndices.Count;
        while (num2 > 0)
        {
            var num3 = MathUtils.Min(num2, 196605);
            Display.DrawUserIndexed(PrimitiveType.TriangleList, shader, VertexPositionColorTexture.VertexDeclaration,
                TriangleVertices.Array, 0, TriangleVertices.Count, TriangleIndices.Array, num, num3);
            num += num3;
            num2 -= num3;
        }

        if (clearAfterFlush)
        {
            Clear();
        }
    }

    public void FlushWithDeviceState(bool useAlphaTest, Texture2D texture, SamplerState samplerState, Matrix matrix,
        Vector4 color, bool clearAfterFlush = true)
    {
        if (useAlphaTest)
        {
            ShaderAlphaTest.Texture = texture;
            ShaderAlphaTest.SamplerState = samplerState;
            ShaderAlphaTest.Transforms.World[0] = matrix;
            ShaderAlphaTest.Color = color;
            ShaderAlphaTest.AlphaThreshold = 0f;
            FlushWithDeviceState(ShaderAlphaTest, clearAfterFlush);
        }
        else
        {
            Shader.Texture = texture;
            Shader.SamplerState = samplerState;
            Shader.Transforms.World[0] = matrix;
            Shader.Color = color;
            FlushWithDeviceState(Shader, clearAfterFlush);
        }
    }

    public void FlushWithDeviceState(Shader shader, bool clearAfterFlush = true)
    {
        var num = 0;
        var num2 = TriangleIndices.Count;
        while (num2 > 0)
        {
            var num3 = MathUtils.Min(num2, 196605);
            Display.DrawUserIndexed(PrimitiveType.TriangleList, shader, VertexPositionColorTexture.VertexDeclaration,
                TriangleVertices.Array, 0, TriangleVertices.Count, TriangleIndices.Array, num, num3);
            num += num3;
            num2 -= num3;
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
}
