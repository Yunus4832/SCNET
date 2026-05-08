using Engine.Core;

namespace Engine.Graphics;

public struct VertexPositionColor(Vector3 position, Color color)
{
    public static readonly VertexDeclaration VertexDeclaration =
        new(new VertexElement(0, VertexElementFormat.Vector3, "POSITION"),
            new VertexElement(12, VertexElementFormat.NormalizedByte4, "COLOR"));

    public Vector3 Position = position;

    public Color Color = color;
}
