using Engine.Graphics;

namespace Game.Terrains.Rendering;

/// <summary>同帧上传的顶点光照响应与发光材质。</summary>
internal readonly record struct TerrainVertexLighting(TerrainLightResponse Response, Color EmissiveMaterial)
{
    internal static VertexDeclaration VertexDeclaration { get; } = new(
        new VertexElement(0, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate1),
        new VertexElement(4, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate2),
        new VertexElement(8, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate3),
        new VertexElement(12, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate4),
        new VertexElement(16, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate5)
    );
}
