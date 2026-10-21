using Engine.Core.Mathematics;

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
}
