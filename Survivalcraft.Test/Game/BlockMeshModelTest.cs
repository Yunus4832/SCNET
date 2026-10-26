using Engine.Core.Mathematics;
using Engine.Graphics;
using Engine.Media.Models;

using Game.Blocks;

namespace Survivalcraft.Test;

public sealed class BlockMeshModelTest
{
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
