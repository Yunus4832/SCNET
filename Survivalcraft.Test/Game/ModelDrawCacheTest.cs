using System.Buffers.Binary;

using Engine.Core.Runtime;
using Engine.Graphics;
using Engine.Media.Models;

using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test;

[Collection(ConfigFileCollection.Name)]
public sealed class ModelDrawCacheTest
{
    [Fact]
    public void SharedMeshNodesHaveDistinctDrawRangesAndOrderSpecificCacheEntries()
    {
        var previous = RunMode.Value;
        RunMode.Value = RunModeType.HeadlessServer;
        try
        {
            var data = new ModelData();
            var bytes = new byte[36];
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 1);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(28), 1);
            data.Accessors.Add(
                new ModelAccessorData
                    { ComponentType = ModelComponentType.Float, Dimensions = "VEC3", Count = 3, Data = bytes }
            );
            var sourceMesh = new ModelMeshData { Name = "shared geometry" };
            var primitive = new ModelMeshPartData();
            primitive.Attributes.Add("POSITION", 0);
            sourceMesh.MeshParts.Add(primitive);
            data.Meshes.Add(sourceMesh);
            data.Nodes.Add(new ModelNodeData { Name = "first", MeshIndex = 0 });
            data.Nodes.Add(new ModelNodeData { Name = "second", MeshIndex = 0 });
            var scene = new ModelSceneData();
            scene.RootNodes.AddRange([0, 1]);
            data.Scenes.Add(scene);
            using var model = Model.Load(data, true);
            var order = new[] { 0, 1 };
            var first = InstancedModelsManager.GetInstancedModelData(model, order);
            Assert.Equal(6, first.VertexBuffer.VerticesCount);
            Assert.Equal(6, first.IndexBuffer.IndicesCount);
            order[0] = 1;
            order[1] = 0;
            var reversed = InstancedModelsManager.GetInstancedModelData(model, order);
            Assert.NotSame(first, reversed);
            Assert.Same(first, InstancedModelsManager.GetInstancedModelData(model, [0, 1]));
            model.Dispose();
            Assert.Throws<InvalidOperationException>(() => first.VertexBuffer.SetData(Array.Empty<byte>(), 0, 0));
            Assert.Throws<InvalidOperationException>(() => reversed.IndexBuffer.SetData(Array.Empty<byte>(), 0, 0));
        }
        finally
        {
            RunMode.Value = previous;
        }
    }
}
