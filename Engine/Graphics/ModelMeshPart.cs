using Engine.Core;

namespace Engine.Graphics;

public class ModelMeshPart : IDisposable
{
    public BoundingBox BoundingBox;

    public string TexturePath = string.Empty;

    public required VertexBuffer VertexBuffer { get; set; }

    public required IndexBuffer IndexBuffer { get; set; }

    public required int StartIndex { get; set; }

    public required int IndicesCount { get; set; }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        VertexBuffer = null!;

        IndexBuffer.Dispose();
        IndexBuffer = null!;
    }
}
