using System.Buffers.Binary;

using Engine.Core.Mathematics;
using Engine.Graphics;
using Engine.Media.Models;

using Game.Blocks.Presentation.Geometry;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test;

[Collection(ConfigFileCollection.Name)]
public sealed class BlockMeshModelTest
{
    [Fact]
    public void BlockCollisionGeometryInGuiModeDoesNotRequestGpuBuffers()
    {
        var data = new ModelData();
        var bytes = new byte[36];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(28), 1);
        data.Accessors.Add(
            new ModelAccessorData
                { ComponentType = ModelComponentType.Float, Dimensions = "VEC3", Count = 3, Data = bytes }
        );
        var mesh = new ModelMeshData();
        var source = new ModelMeshPartData();
        source.Attributes.Add("POSITION", 0);
        mesh.MeshParts.Add(source);
        data.Meshes.Add(mesh);
        data.Nodes.Add(new ModelNodeData { MeshIndex = 0 });
        var scene = new ModelSceneData();
        scene.RootNodes.Add(0);
        data.Scenes.Add(scene);
        using var model = Model.Load(data);
        var primitive = model.MeshNodes[0].RequireMesh().Primitives[0];
        var block = new BlockMesh();

        block.AppendModelPrimitive(
            primitive,
            Matrix.Identity,
            false,
            false,
            false,
            false,
            Color.White
        );

        Assert.Equal(3, block.Vertices.Count);
        Assert.Equal(3, block.Indices.Count);
        Assert.False(primitive.HasGraphicsResources);
        var bounds = block.CalculateBoundingBox();
        Assert.True(bounds.Max.X >= 1 && bounds.Max.Y >= 1);
    }

    [Fact]
    public void BlockAndModelPathsEvaluateRotatedParentHierarchyIdentically()
    {
        var data = new ModelData();
        var root = new ModelNodeData { Name = "parent", MatrixTransform = Matrix.CreateRotationZ(MathF.PI / 2) };
        root.Children.Add(1);
        data.Nodes.Add(root);
        data.Nodes.Add(new ModelNodeData { Name = "child", Translation = Vector3.UnitX });
        var scene = new ModelSceneData();
        scene.RootNodes.Add(0);
        data.Scenes.Add(scene);
        using var model = Model.Load(data);
        var child = model.FindNode("child")!;
        var transforms = new Matrix[2];
        model.CopyAbsoluteNodeTransformsTo(transforms);
        var block = Vector3.Transform(Vector3.Zero, BlockMesh.GetNodeAbsoluteTransform(child));
        var expected = Vector3.Transform(Vector3.Zero, transforms[child.Index]);
        Assert.True((block - expected).Length() < 0.00001f);
        Assert.True((block - Vector3.UnitY).Length() < 0.00001f);
    }
}
