using Engine.Graphics;

namespace Game.Managers;

public static class InstancedModelsManager
{
    private static readonly Dictionary<Model, InstancedModelData> _cache;

    static InstancedModelsManager()
    {
        _cache = new Dictionary<Model, InstancedModelData>();
        Display.DeviceReset += delegate
        {
            foreach (var value in _cache.Values)
            {
                value.VertexBuffer.Dispose();
                value.IndexBuffer.Dispose();
            }

            _cache.Clear();
        };
    }

    public static InstancedModelData GetInstancedModelData(Model model, int[] meshDrawOrders)
    {
        if (_cache.TryGetValue(model, out var value))
        {
            return value;
        }

        value = CreateInstancedModelData(model, meshDrawOrders);
        _cache.Add(model, value);

        return value;
    }

    private static InstancedModelData CreateInstancedModelData(Model model, int[] meshDrawOrders)
    {
        var dynamicArray = new DynamicArray<InstancedVertex>();
        var dynamicArray2 = new DynamicArray<ushort>();
        foreach (var order in meshDrawOrders)
        {
            var modelMesh = model.Meshes[order];
            foreach (var meshPart in modelMesh.MeshParts)
            {
                _ = dynamicArray.Count;
                var vertexBuffer = meshPart.VertexBuffer;
                var indexBuffer = meshPart.IndexBuffer;
                var vertexElements = vertexBuffer.VertexDeclaration.VertexElements;
                var indexData = BlockMesh.GetIndexData<ushort>(indexBuffer);
                var dictionary = new Dictionary<ushort, ushort>();
                if (vertexElements.Count != 3 || vertexElements[0].Offset != 0 ||
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

                    dictionary.Add(num, (ushort)dynamicArray.Count);
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
                        Instance = modelMesh.ParentBone?.Index ?? 0f
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
