using Engine.Core;

namespace Engine.Media;

public class ModelMeshData
{
    public BoundingBox BoundingBox;

    public readonly List<ModelMeshPartData> MeshParts = [];

    public string Name = string.Empty;

    public int ParentBoneIndex;
}
