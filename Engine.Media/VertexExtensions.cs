namespace Engine.Media;

public static class VertexExtensions
{
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
