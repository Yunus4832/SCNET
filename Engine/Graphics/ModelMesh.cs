using Engine.Core;

namespace Engine.Graphics;

public class ModelMesh : IDisposable
{
    public BoundingBox BoundingBox;

    private readonly List<ModelMeshPart> _meshParts = [];

    public required string Name { get; set; }

    public ModelBone? ParentBone { get; set; }

    public ReadOnlyList<ModelMeshPart> MeshParts => new(_meshParts);

    public void Dispose()
    {
        Utilities.DisposeCollection(_meshParts);
    }

    public ModelMeshPart NewMeshPart(
        VertexBuffer vertexBuffer,
        IndexBuffer indexBuffer,
        int startIndex,
        int indicesCount,
        BoundingBox boundingBox
    )
    {
        if (startIndex < 0 || indicesCount < 0 || startIndex + indicesCount > indexBuffer.IndicesCount)
        {
            throw new InvalidOperationException("Specified range is outside of index buffer.");
        }

        var modelMeshPart = new ModelMeshPart
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            StartIndex = startIndex,
            IndicesCount = indicesCount,
            BoundingBox = boundingBox,
        };
        _meshParts.Add(modelMeshPart);
        return modelMeshPart;
    }
}
