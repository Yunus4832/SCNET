using Engine.Core;

namespace Engine.Graphics;

public static class ExtensionMethods
{
    public static int GetSize(this IndexFormat format)
    {
        return format switch
        {
            IndexFormat.SixteenBits => 2,
            IndexFormat.ThirtyTwoBits => 4,
            _ => throw new InvalidOperationException("Unsupported IndexFormat.")
        };
    }

    public static string GetSemanticString(this VertexElementSemantic semantic)
    {
        return semantic switch
        {
            VertexElementSemantic.Position => "POSITION",
            VertexElementSemantic.Color => "COLOR",
            VertexElementSemantic.Normal => "NORMAL",
            VertexElementSemantic.TextureCoordinate => "TEXCOORD",
            VertexElementSemantic.TextureCoordinate0 => "TEXCOORD0",
            VertexElementSemantic.TextureCoordinate1 => "TEXCOORD1",
            VertexElementSemantic.TextureCoordinate2 => "TEXCOORD2",
            VertexElementSemantic.TextureCoordinate3 => "TEXCOORD3",
            VertexElementSemantic.TextureCoordinate4 => "TEXCOORD4",
            VertexElementSemantic.TextureCoordinate5 => "TEXCOORD5",
            VertexElementSemantic.TextureCoordinate6 => "TEXCOORD6",
            VertexElementSemantic.TextureCoordinate7 => "TEXCOORD7",
            VertexElementSemantic.Instance => "INSTANCE",
            VertexElementSemantic.BlendIndices => "BLENDINDICES",
            VertexElementSemantic.BlendWeights => "BLENDWEIGHTS",
            _ => throw new InvalidOperationException("Unrecognized vertex semantic.")
        };
    }

    public static int GetSize(this ColorFormat format)
    {
        return format switch
        {
            ColorFormat.Rgba8888 => 4,
            ColorFormat.Rgb565 => 2,
            ColorFormat.Rgba5551 => 2,
            ColorFormat.R8 => 1,
            _ => throw new InvalidOperationException("Unsupported ColorFormat.")
        };
    }

    public static int GetSize(this DepthFormat format)
    {
        return format switch
        {
            DepthFormat.None => 0,
            DepthFormat.Depth16 => 2,
            DepthFormat.Depth24Stencil8 => 4,
            _ => throw new InvalidOperationException("Unsupported DepthFormat.")
        };
    }

    public static int GetPrimitivesCount(this PrimitiveType primitiveType, int indicesCount)
    {
        return primitiveType switch
        {
            PrimitiveType.LineList => indicesCount / 2,
            PrimitiveType.LineStrip => MathUtils.Max(indicesCount - 1, 0),
            PrimitiveType.TriangleList => indicesCount / 3,
            PrimitiveType.TriangleStrip => MathUtils.Max(indicesCount - 2, 0),
            _ => throw new InvalidOperationException("Unsupported PrimitiveType.")
        };
    }

    public static int GetSize(this ShaderParameterType type)
    {
        return type switch
        {
            ShaderParameterType.Float => 4,
            ShaderParameterType.Vector2 => 8,
            ShaderParameterType.Vector3 => 12,
            ShaderParameterType.Vector4 => 16,
            ShaderParameterType.Matrix => 64,
            _ => throw new InvalidOperationException("Unsupported ShaderParameterType.")
        };
    }

    public static int GetElementsCount(this VertexElementFormat format)
    {
        return format switch
        {
            VertexElementFormat.Single => 1,
            VertexElementFormat.Vector2 => 2,
            VertexElementFormat.Vector3 => 3,
            VertexElementFormat.Vector4 => 4,
            VertexElementFormat.Byte4 => 4,
            VertexElementFormat.NormalizedByte4 => 4,
            VertexElementFormat.Short2 => 2,
            VertexElementFormat.NormalizedShort2 => 2,
            VertexElementFormat.Short4 => 4,
            VertexElementFormat.NormalizedShort4 => 4,
            _ => throw new InvalidOperationException("Unsupported VertexElementFormat.")
        };
    }

    public static int GetElementSize(this VertexElementFormat format)
    {
        return format switch
        {
            VertexElementFormat.Single => 4,
            VertexElementFormat.Vector2 => 4,
            VertexElementFormat.Vector3 => 4,
            VertexElementFormat.Vector4 => 4,
            VertexElementFormat.Byte4 => 1,
            VertexElementFormat.NormalizedByte4 => 1,
            VertexElementFormat.Short2 => 2,
            VertexElementFormat.NormalizedShort2 => 2,
            VertexElementFormat.Short4 => 2,
            VertexElementFormat.NormalizedShort4 => 2,
            _ => throw new InvalidOperationException("Unsupported VertexElementFormat.")
        };
    }

    public static int GetSize(this VertexElementFormat format)
    {
        return format.GetElementsCount() * format.GetElementSize();
    }
}
