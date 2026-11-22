using Engine.Core.Collections;
using Engine.Core.Mathematics;
using Engine.Media.Models;

namespace Engine.Graphics;

/// <summary>源网格在一组固定默认形变权重下共享的 CPU 几何数据。</summary>
public sealed class ModelMesh
{
    private readonly List<ModelPrimitive> _primitives = [];

    private readonly float[] _morphWeights;

    internal ModelMesh(ModelData data, int sourceMeshIndex, float[] morphWeights)
    {
        var source = data.Meshes[sourceMeshIndex];
        Name = source.Name;
        SourceMeshIndex = sourceMeshIndex;
        _morphWeights = morphWeights;
        try
        {
            foreach (var primitive in source.MeshParts)
            {
                var geometry = ModelGeometryData.Create(data, primitive, morphWeights);
                _primitives.Add(new ModelPrimitive(geometry, primitive.MaterialIndex));
            }
        }
        catch
        {
            ReleaseBuffers();
            throw;
        }

        BoundingBox = _primitives.Count == 0
            ? source.BoundingBox
            : _primitives.Select(p => p.BoundingBox).Aggregate(BoundingBox.Union);
    }

    public string Name { get; }

    public int SourceMeshIndex { get; }

    public ReadOnlyList<float> MorphWeights => new(_morphWeights);

    public ReadOnlyList<ModelPrimitive> Primitives => new(_primitives);

    public BoundingBox BoundingBox { get; }

    internal void ReleaseBuffers()
    {
        foreach (var primitive in _primitives)
        {
            primitive.ReleaseBuffers();
        }

        _primitives.Clear();
    }
}
