using Engine.Graphics;

using Game.Terrains.Rendering;

namespace Game.Terrains;

public struct TerrainVertex
{
    public float X;

    public float Y;

    public float Z;

    public short Tx;

    public short Ty;

    public Color Color;

    public TerrainLightResponse Lighting;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementSemantic.Position),
        new VertexElement(12, VertexElementFormat.NormalizedShort2, VertexElementSemantic.TextureCoordinate),
        new VertexElement(16, VertexElementFormat.NormalizedByte4, VertexElementSemantic.Color),
        new VertexElement(20, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate1),
        new VertexElement(24, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate2),
        new VertexElement(28, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate3),
        new VertexElement(32, VertexElementFormat.NormalizedByte4, VertexElementSemantic.TextureCoordinate4)
    );
}
