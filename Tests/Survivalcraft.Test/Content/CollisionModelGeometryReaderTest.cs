using System.Buffers.Binary;

using Engine.Core.Mathematics;
using Engine.Graphics;
using Engine.Media.Models;

using Game.Blocks.Collision;
using Game.Blocks.Presentation.Geometry;
using Game.Content;
using Game.Modding.Content;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class CollisionModelGeometryReaderTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IndexedMorphPositionsMatchExistingMeshesUnderNonAxisAlignedTransforms(bool normals)
    {
        var data = CreateModel(normals);
        data.Nodes[1].MorphWeights = [0.25f];
        data.Nodes[0].Children.Add(1);
        data.Nodes[0].Translation = new Vector3(3f, -2f, 1f);
        data.Nodes[0].Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.37f);
        data.Nodes[1].Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.23f);
        using var graphics = Model.Load(data);
        var collision = CollisionModelGeometryReader.Convert(ResourceId.Parse("game:Models/Test.glb"), data);
        AssertMatches(graphics, collision);
    }

    [Theory]
    [InlineData("Models/IronDoor.glb")]
    [InlineData("Models/IronSign.glb")]
    [InlineData("Models/PressurePlate.glb")]
    public void ActualBuiltInGeometryMatchesTheExistingIndexedMeshes(string path)
    {
        var asset = BuiltInContentAssets.Load().Single(asset => asset.RelativePath == path);
        using var stream = asset.OpenRead();
        var data = ModelData.Load(stream);
        using var graphics = Model.Load(data);
        AssertMatches(graphics, CollisionModelGeometryReader.Convert(ResourceId.Parse("game:" + path), data));
    }

    [Fact]
    public void SelectedSceneAndAmbiguousNamesMatchTheModelContract()
    {
        var data = CreateModel(true);
        data.Scenes.Add(new ModelSceneData());
        data.Scenes[1].RootNodes.Add(1);
        data.DefaultSceneIndex = 1;
        var collision = CollisionModelGeometryReader.Convert(ResourceId.Parse("game:Models/Test.glb"), data);
        Assert.Equal(1, Assert.Single(collision.Nodes).SourceNodeIndex);
        data.Nodes.Add(new ModelNodeData { Name = "Part", MeshIndex = 0 });
        data.Scenes[1].RootNodes.Add(2);
        collision = CollisionModelGeometryReader.Convert(ResourceId.Parse("game:Models/Test.glb"), data);
        Assert.Throws<InvalidOperationException>(() => collision.GetNode("Part"));
        using var graphics = Model.Load(data);
        Assert.Throws<InvalidOperationException>(() => graphics.FindNode("Part"));
    }

    [Fact]
    public void EmptySceneIsRejectedBeforeAnyGeometryIsCreated()
    {
        var data = new ModelData();
        Assert.Throws<InvalidDataException>(() =>
            CollisionModelGeometryReader.Convert(ResourceId.Parse("game:Models/Empty.glb"), data)
        );
        data.Scenes.Add(new ModelSceneData());
        Assert.Throws<InvalidDataException>(() =>
            CollisionModelGeometryReader.Convert(ResourceId.Parse("game:Models/Empty.glb"), data)
        );
    }

    private static void AssertMatches(Model model, CollisionModelGeometry collision)
    {
        var expected = new BlockMesh();
        var actual = new CollisionGeometryBuilder();
        var transform = Matrix.CreateRotationY(0.63f) *
                        Matrix.CreateRotationZ(0.19f) *
                        Matrix.CreateTranslation(new Vector3(-2f, 4f, 7f));
        foreach (var node in model.Nodes)
        {
            var collisionNode = collision.Nodes.Single(candidate => candidate.SourceNodeIndex == node.SourceNodeIndex);
            Assert.Equal(BlockMesh.GetNodeAbsoluteTransform(node), collisionNode.AbsoluteTransform);
            if (node.Mesh is null) { continue; }

            Assert.Equal(node.Mesh.Primitives.Count, collisionNode.Primitives.Count);
            for (var primitive = 0; primitive < node.Mesh.Primitives.Count; primitive++)
            {
                expected.AppendModelPrimitive(
                    node.Mesh.Primitives[primitive],
                    collisionNode.AbsoluteTransform * transform,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                actual.Append(collisionNode.Primitives[primitive], collisionNode.AbsoluteTransform * transform);
            }
        }

        Assert.Equal(expected.Vertices.Count, actual.Count);
        Assert.Equal(expected.CalculateBoundingBox(), actual.GetBounds());
        var secondTransform = Matrix.CreateRotationX(0.47f);
        Assert.Equal(expected.CalculateBoundingBox(secondTransform), actual.GetBounds(secondTransform));
    }

    private static ModelData CreateModel(bool normals)
    {
        var model = new ModelData();
        model.Accessors.Add(Accessor([new(0f, 0f, 0f), new(2f, 0f, 0f), new(0f, 1f, 1f)]));
        model.Accessors.Add(Accessor([Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ]));
        model.Accessors.Add(Accessor([Vector3.UnitY, Vector3.UnitY, Vector3.UnitY]));
        var mesh = new ModelMeshData { MorphWeights = [0.75f] };
        for (var primitive = 0; primitive < 2; primitive++)
        {
            var part = new ModelMeshPartData();
            part.Attributes.Add("POSITION", 0);
            if (normals) { part.Attributes.Add("NORMAL", 1); }

            part.MorphTargets.Add(new Dictionary<string, int> { ["POSITION"] = 2 });
            mesh.MeshParts.Add(part);
        }

        model.Meshes.Add(mesh);
        model.Nodes.Add(new ModelNodeData { Name = "Root" });
        model.Nodes.Add(new ModelNodeData { Name = "Part", MeshIndex = 0 });
        model.Scenes.Add(new ModelSceneData());
        model.Scenes[0].RootNodes.Add(0);
        return model;
    }

    private static ModelAccessorData Accessor(Vector3[] positions)
    {
        var data = new byte[positions.Length * 12];
        for (var i = 0; i < positions.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 12), positions[i].X);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 12 + 4), positions[i].Y);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 12 + 8), positions[i].Z);
        }

        return new ModelAccessorData
            { ComponentType = ModelComponentType.Float, Dimensions = "VEC3", Count = positions.Length, Data = data };
    }
}
