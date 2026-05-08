using Engine.Graphics;

namespace Engine.Media;

public class ModelBuffersData
{
    public byte[] Indices = [];

    public required VertexDeclaration VertexDeclaration;

    public byte[] Vertices = [];
}
