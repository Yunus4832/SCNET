using Engine.Core.Mathematics;

namespace Engine.Media.Models;

public sealed class ModelMeshData : ModelElementData
{
    public BoundingBox BoundingBox;

    public readonly List<ModelMeshPartData> MeshParts = [];

    public float[] MorphWeights = [];
}
