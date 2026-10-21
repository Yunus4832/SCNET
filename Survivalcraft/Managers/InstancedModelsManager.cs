using Engine.Graphics;

namespace Game.Managers;

public static class InstancedModelsManager
{
    private static readonly Dictionary<Model, List<CacheEntry>> _cache = new();

    static InstancedModelsManager()
    {
        Display.DeviceReset += () =>
        {
            foreach (var model in _cache.Keys.ToArray())
            {
                ReleaseModel(model);
            }
        };
    }

    public static InstancedModelData GetInstancedModelData(Model model, int[] drawNodeIndices)
    {
        ObjectDisposedException.ThrowIf(model.IsDisposed, model);
        if (!_cache.TryGetValue(model, out var entries))
        {
            entries = [];
            _cache.Add(model, entries);
            model.Disposed += ReleaseModel;
        }

        var entry = entries.FirstOrDefault(e => e.DrawNodeIndices.SequenceEqual(drawNodeIndices));
        if (entry != null)
        {
            return entry.Data;
        }

        var data = CreateInstancedModelData(model, drawNodeIndices);
        entries.Add(new CacheEntry(drawNodeIndices.ToArray(), data));
        return data;
    }

    private static void ReleaseModel(Model model)
    {
        model.Disposed -= ReleaseModel;
        if (_cache.Remove(model, out var entries))
        {
            foreach (var entry in entries)
            {
                entry.Data.VertexBuffer.Dispose();
                entry.Data.IndexBuffer.Dispose();
            }
        }
    }

    private static InstancedModelData CreateInstancedModelData(Model model, int[] drawNodeIndices)
    {
        var dynamicArray = new DynamicArray<InstancedVertex>();
        var dynamicArray2 = new DynamicArray<ushort>();
        foreach (var order in drawNodeIndices)
        {
            var node = model.Nodes[order];
            foreach (var meshPart in node.RequireMesh().Primitives)
            {
                _ = dynamicArray.Count;
                var vertexBuffer = meshPart.VertexBuffer;
                var indexBuffer = meshPart.IndexBuffer;
                if (indexBuffer.IndexFormat != IndexFormat.SixteenBits)
                {
                    throw new NotSupportedException("Procedural creature rendering requires 16-bit mesh batches.");
                }

                var vertexElements = vertexBuffer.VertexDeclaration.VertexElements;
                var indexData = BlockMesh.GetIndexData<ushort>(indexBuffer);
                var dictionary = new Dictionary<ushort, ushort>();
                if (vertexElements.Count != 3 ||
                    vertexElements[0].Offset != 0 ||
                    vertexElements[0].Semantic != VertexElementSemantic.Position.GetSemanticString() ||
                    vertexElements[1].Offset != 12 ||
                    vertexElements[1].Semantic != VertexElementSemantic.Normal.GetSemanticString() ||
                    vertexElements[2].Offset != 24 ||
                    vertexElements[2].Semantic != VertexElementSemantic.TextureCoordinate.GetSemanticString())
                {
                    throw new InvalidOperationException("Unsupported vertex format.");
                }

                var vertexData = BlockMesh.GetVertexData<SourceModelVertex>(vertexBuffer);
                for (var j = meshPart.StartIndex; j < meshPart.StartIndex + meshPart.IndicesCount; j++)
                {
                    var num = indexData[j];
                    if (dictionary.ContainsKey(num))
                    {
                        continue;
                    }

                    dictionary.Add(num, checked((ushort)dynamicArray.Count));
                    var sourceModelVertex = vertexData[num];
                    var item = new InstancedVertex
                    {
                        X = sourceModelVertex.X,
                        Y = sourceModelVertex.Y,
                        Z = sourceModelVertex.Z,
                        Nx = sourceModelVertex.Nx,
                        Ny = sourceModelVertex.Ny,
                        Nz = sourceModelVertex.Nz,
                        Tx = sourceModelVertex.Tx,
                        Ty = sourceModelVertex.Ty,
                        Instance = node.Index
                    };
                    dynamicArray.Add(item);
                }

                for (var k = 0; k < meshPart.IndicesCount / 3; k++)
                {
                    dynamicArray2.Add(dictionary[indexData[meshPart.StartIndex + 3 * k]]);
                    dynamicArray2.Add(dictionary[indexData[meshPart.StartIndex + 3 * k + 1]]);
                    dynamicArray2.Add(dictionary[indexData[meshPart.StartIndex + 3 * k + 2]]);
                }
            }
        }

        var instancedModelData = new InstancedModelData
        {
            VertexBuffer = new VertexBuffer(InstancedModelData.VertexDeclaration, dynamicArray.Count),
            IndexBuffer = new IndexBuffer(IndexFormat.SixteenBits, dynamicArray2.Count)
        };
        instancedModelData.VertexBuffer.SetData(dynamicArray.Array, 0, dynamicArray.Count);
        instancedModelData.IndexBuffer.SetData(dynamicArray2.Array, 0, dynamicArray2.Count);
        return instancedModelData;
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private struct SourceModelVertex
    {
        public float X;

        public float Y;

        public float Z;

        public float Nx;

        public float Ny;

        public float Nz;

        public float Tx;

        public float Ty;
    }

    private sealed record CacheEntry(int[] DrawNodeIndices, InstancedModelData Data);

    private struct InstancedVertex
    {
        public float X;

        public float Y;

        public float Z;

        public float Nx;

        public float Ny;

        public float Nz;

        public float Tx;

        public float Ty;

        public float Instance;
    }
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}
