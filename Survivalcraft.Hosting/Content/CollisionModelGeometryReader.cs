using Engine.Media.Models;

using Game.Blocks.Collision;

namespace Game.Content;

/// <summary>Projects the CPU model format into the immutable geometry needed by block collision rules.</summary>
public static class CollisionModelGeometryReader
{
    public static CollisionModelGeometry Read(ResourceId id, Stream stream)
    {
        return Convert(id, ModelData.Load(stream));
    }

    public static CollisionModelGeometry Convert(ResourceId id, ModelData model)
    {
        if (model.Scenes.Count == 0)
        {
            throw new InvalidDataException("Collision model has no scene.");
        }

        var scene = model.Scenes[model.DefaultSceneIndex < 0 ? 0 : model.DefaultSceneIndex];
        if (scene.RootNodes.Count == 0)
        {
            throw new InvalidDataException("Selected collision scene has no nodes.");
        }

        var nodes = new List<CollisionNodeGeometry>();
        var visited = new HashSet<int>();
        foreach (var root in scene.RootNodes) { Visit(root, Matrix.Identity); }

        return new CollisionModelGeometry(id, nodes);

        void Visit(int index, Matrix parentTransform)
        {
            if (!visited.Add(index))
            {
                throw new InvalidDataException("A collision scene node cannot have multiple parents or form a cycle.");
            }

            var source = model.Nodes[index];
            if (source.SkinIndex >= 0)
            {
                throw new NotSupportedException("Rigid block collision cannot evaluate model skins.");
            }

            var absoluteTransform = source.Transform * parentTransform;
            var primitives = new List<CollisionPrimitiveGeometry>();
            if (source.MeshIndex >= 0)
            {
                var mesh = model.Meshes[source.MeshIndex];
                var targets = mesh.MeshParts.Count == 0 ? 0 : mesh.MeshParts[0].MorphTargets.Count;
                var sourceWeights = source.MorphWeights ?? mesh.MorphWeights;
                if (sourceWeights.Length > targets || mesh.MeshParts.Any(part => part.MorphTargets.Count != targets))
                {
                    throw new InvalidDataException("Collision mesh morph target and weight counts are inconsistent.");
                }

                var weights = new float[targets];
                sourceWeights.CopyTo(weights, 0);
                foreach (var primitive in mesh.MeshParts)
                {
                    primitives.Add(ReadPrimitive(model, primitive, weights));
                }
            }

            nodes.Add(new CollisionNodeGeometry(source.Name, index, absoluteTransform, primitives));
            foreach (var child in source.Children) { Visit(child, absoluteTransform); }
        }
    }

    private static CollisionPrimitiveGeometry ReadPrimitive(
        ModelData model,
        ModelMeshPartData primitive,
        IReadOnlyList<float> weights
    )
    {
        if (!primitive.Attributes.TryGetValue("POSITION", out var positionIndex))
        {
            throw new InvalidDataException("Collision primitive requires POSITION data.");
        }

        var accessor = model.Accessors[positionIndex];
        var positions = Enumerable.Range(0, accessor.Count).Select(accessor.GetVector3).ToArray();
        for (var target = 0; target < weights.Count; target++)
        {
            if (weights[target] == 0f || !primitive.MorphTargets[target].TryGetValue("POSITION", out var delta))
            {
                continue;
            }

            for (var vertex = 0; vertex < positions.Length; vertex++)
            {
                positions[vertex] += model.Accessors[delta].GetVector3(vertex) * weights[target];
            }
        }

        var sourceIndices = primitive.IndicesAccessorIndex < 0
            ? Enumerable.Range(0, positions.Length).ToArray()
            : Enumerable.Range(0, model.Accessors[primitive.IndicesAccessorIndex].Count)
                .Select(index => checked((int)model.Accessors[primitive.IndicesAccessorIndex].GetComponent(index, 0)))
                .ToArray();
        var triangles = Triangles(sourceIndices, primitive.Topology);
        var indexedPositions = new List<Vector3>();
        var hasNormals = primitive.Attributes.ContainsKey("NORMAL");
        var visited = new HashSet<int>();
        for (var triangle = 0; triangle < triangles.Length; triangle += 3)
        {
            // Match the existing rigid renderer's winding and its flat-normal vertex expansion.
            Add(triangles[triangle]);
            Add(triangles[triangle + 2]);
            Add(triangles[triangle + 1]);
        }

        return new CollisionPrimitiveGeometry(indexedPositions);

        void Add(int index)
        {
            if (!hasNormals || visited.Add(index)) { indexedPositions.Add(positions[index]); }
        }
    }

    private static int[] Triangles(int[] indices, ModelPrimitiveTopology topology)
    {
        if (topology == ModelPrimitiveTopology.Triangles)
        {
            if (indices.Length % 3 != 0)
            {
                throw new InvalidDataException("Triangle indices must be a multiple of three.");
            }

            return indices;
        }

        if (topology is not (ModelPrimitiveTopology.TriangleStrip or ModelPrimitiveTopology.TriangleFan))
        {
            throw new NotSupportedException($"Block collision does not support {topology} primitives.");
        }

        var triangles = new List<int>();
        for (var index = 2; index < indices.Length; index++)
        {
            triangles.Add(topology == ModelPrimitiveTopology.TriangleFan ? indices[0] : indices[index - 2]);
            triangles.Add(
                topology == ModelPrimitiveTopology.TriangleStrip && index % 2 != 0 ? indices[index] : indices[index - 1]
            );
            triangles.Add(
                topology == ModelPrimitiveTopology.TriangleStrip && index % 2 != 0 ? indices[index - 1] : indices[index]
            );
        }

        return triangles.ToArray();
    }
}
