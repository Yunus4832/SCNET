using Engine.Core.Mathematics;

namespace Engine.Media.Models;

public sealed class ModelMeshPartData : ModelElementData
{
    public BoundingBox BoundingBox;

    public readonly Dictionary<string, int> Attributes = new(StringComparer.Ordinal);

    public readonly List<Dictionary<string, int>> MorphTargets = [];

    public int IndicesAccessorIndex = -1;

    public int MaterialIndex = -1;

    public ModelPrimitiveTopology Topology = ModelPrimitiveTopology.Triangles;
}
