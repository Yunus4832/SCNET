using Engine.Core.Mathematics;

namespace Engine.Graphics;

/// <summary>A drawing range in buffers owned by its mesh. Nodes and primitives do not dispose shared buffers.</summary>
public sealed class ModelPrimitive
{
    internal ModelPrimitive(VertexBuffer vertices, IndexBuffer indices, int materialIndex, BoundingBox bounds)
    {
        VertexBuffer = vertices;
        IndexBuffer = indices;
        MaterialIndex = materialIndex;
        BoundingBox = bounds;
    }

    public VertexBuffer VertexBuffer { get; }

    public IndexBuffer IndexBuffer { get; }

    public int MaterialIndex { get; }

    public BoundingBox BoundingBox { get; }

    public int StartIndex => 0;

    public int IndicesCount => IndexBuffer.IndicesCount;
}
