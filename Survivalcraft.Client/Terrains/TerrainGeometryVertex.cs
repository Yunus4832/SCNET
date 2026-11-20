using Engine.Graphics;

namespace Game.Terrains;

internal readonly record struct TerrainGeometryVertex(Vector3 Position, short Tx, short Ty, Color Color)
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementSemantic.Position),
        new VertexElement(12, VertexElementFormat.NormalizedShort2, VertexElementSemantic.TextureCoordinate),
        new VertexElement(16, VertexElementFormat.NormalizedByte4, VertexElementSemantic.Color)
    );
}
