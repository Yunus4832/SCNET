using System.Buffers.Binary;

using Engine.Core.Mathematics;
using Engine.Core.Runtime;
using Engine.Graphics;
using Engine.Media.Models;

namespace Engine.Test.Graphics;

[CollectionDefinition(nameof(ModelResourceCollection), DisableParallelization = true)]
public sealed class ModelResourceCollection;

[Collection(nameof(ModelResourceCollection))]
public sealed class ModelSceneTest : IDisposable
{
    private readonly RunModeType _previous = RunMode.Value;

    public ModelSceneTest()
    {
        RunMode.Value = RunModeType.HeadlessServer;
    }

    public void Dispose()
    {
        RunMode.Value = _previous;
    }

    [Fact]
    public void DuplicateNodeLabelsAreLegalButDoNotRenameOrDuplicateMeshResources()
    {
        var data = Triangle();
        AddRoot(data, "part", 0);
        AddRoot(data, "part", 0);
        using var model = Model.Load(data, true);
        Assert.Equal(2, model.Nodes.Count);
        Assert.Equal(2, model.RootNodes.Count);
        Assert.Equal("geometry", Assert.Single(model.Meshes).Name);
        Assert.Same(model.Nodes[0].Mesh, model.Nodes[1].Mesh);
        Assert.Throws<InvalidOperationException>(() => model.FindNode("part"));
        Assert.Null(model.FindNode("geometry", false));
    }

    [Fact]
    public void SharedBuffersAreReleasedOnlyByTheirModelOwner()
    {
        var count = GraphicsResource.resources.Count;
        var data = Triangle();
        AddRoot(data, "first", 0);
        AddRoot(data, "second", 0);
        var model = Model.Load(data, true);
        var a = model.Nodes[0].RequireMesh().Primitives[0];
        var b = model.Nodes[1].RequireMesh().Primitives[0];
        Assert.Same(a.VertexBuffer, b.VertexBuffer);
        Assert.Same(a.IndexBuffer, b.IndexBuffer);
        Assert.Equal(count + 2, GraphicsResource.resources.Count);
        var notifications = 0;
        model.Disposed += _ => notifications++;
        model.Dispose();
        model.Dispose();
        Assert.Equal(1, notifications);
        Assert.True(a.VertexBuffer.isDisposed);
        Assert.True(a.IndexBuffer.isDisposed);
        Assert.Equal(count, GraphicsResource.resources.Count);
    }

    [Fact]
    public void DefaultMorphVariantsShareOnlyWhenTheirWeightsMatch()
    {
        var data = Triangle();
        var mesh = data.Meshes[0];
        mesh.MorphWeights = [0.5f];
        data.Accessors.Add(Positions([0, 2, 0, 0, 2, 0, 0, 2, 0]));
        mesh.MeshParts[0].MorphTargets.Add(new Dictionary<string, int> { ["POSITION"] = 1 });
        AddRoot(data, "inherited", 0);
        AddRoot(data, "full", 0).MorphWeights = [1];
        AddRoot(data, "same", 0).MorphWeights = [0.5f];
        using var model = Model.Load(data, true);
        Assert.Equal(2, model.Meshes.Count);
        Assert.Same(model.Nodes[0].Mesh, model.Nodes[2].Mesh);
        Assert.NotSame(model.Nodes[0].Mesh, model.Nodes[1].Mesh);
        Assert.Equal(2, model.Nodes[0].RequireMesh().BoundingBox.Max.Y);
        Assert.Equal(3, model.Nodes[1].RequireMesh().BoundingBox.Max.Y);
        Assert.Equal(1, data.Accessors[0].GetVector3(2).Y);
    }

    [Fact]
    public void PosesOwnAnimationStateAndNeverMutateSharedNodeDefinitions()
    {
        var data = new ModelData();
        var root = AddRoot(data, "root", -1);
        root.MatrixTransform = Matrix.CreateRotationZ(MathF.PI / 2);
        data.Nodes.Add(new ModelNodeData { Name = "child", Translation = Vector3.UnitX });
        root.Children.Add(1);
        using var model = Model.Load(data);
        var a = new ModelPose(model);
        var b = new ModelPose(model);
        a.WorldTransform = Matrix.CreateTranslation(10, 0, 0);
        a.SetAnimationTransform(1, Matrix.CreateRotationX(0.5f));
        a.CalculateAbsoluteTransforms(Matrix.Identity);
        b.CalculateAbsoluteTransforms(Matrix.Identity);
        Assert.Null(b.GetAnimationTransform(1));
        Assert.Equal(Vector3.UnitX, model.Nodes[1].LocalTransform.Translation);
        Assert.True((a.AbsoluteTransforms[1].Translation - new Vector3(10, 1, 0)).Length() < 0.00001f);
        Assert.True((b.AbsoluteTransforms[1].Translation - Vector3.UnitY).Length() < 0.00001f);
        Assert.Same(model.Nodes[0], model.Nodes[1].Parent);
    }

    [Fact]
    public void MultipleRootsUseInstancePlacementWithoutInventingANode()
    {
        var data = new ModelData();
        AddRoot(data, "a", -1).Translation = Vector3.UnitX;
        AddRoot(data, "b", -1).Translation = Vector3.UnitY;
        using var model = Model.Load(data);
        Assert.Equal(2, model.Nodes.Count);
        Assert.Equal(2, model.RootNodes.Count);
        var pose = new ModelPose(model) { WorldTransform = Matrix.CreateTranslation(10, 0, 0) };
        pose.CalculateAbsoluteTransforms(Matrix.Identity);
        Assert.Equal(new Vector3(11, 0, 0), pose.AbsoluteTransforms[0].Translation);
        Assert.Equal(new Vector3(10, 1, 0), pose.AbsoluteTransforms[1].Translation);
        model.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pose.CalculateAbsoluteTransforms(Matrix.Identity));
    }

    [Fact]
    public void InvalidGraphCannotLeaveAllocatedMeshResourcesBehind()
    {
        var count = GraphicsResource.resources.Count;
        var data = Triangle();
        AddRoot(data, "mesh", 0);
        AddRoot(data, "cycle", -1).Children.Add(1);
        Assert.Throws<InvalidDataException>(() => Model.Load(data));
        Assert.Equal(count, GraphicsResource.resources.Count);
    }

    private static ModelData Triangle()
    {
        var data = new ModelData();
        data.Accessors.Add(Positions([0, 0, 0, 1, 0, 0, 0, 1, 0]));
        var mesh = new ModelMeshData { Name = "geometry" };
        var primitive = new ModelMeshPartData();
        primitive.Attributes.Add("POSITION", 0);
        mesh.MeshParts.Add(primitive);
        data.Meshes.Add(mesh);
        return data;
    }

    private static ModelAccessorData Positions(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]);
        }

        return new ModelAccessorData
            { ComponentType = ModelComponentType.Float, Dimensions = "VEC3", Count = values.Length / 3, Data = bytes };
    }

    private static ModelNodeData AddRoot(ModelData data, string name, int mesh)
    {
        if (data.Scenes.Count == 0)
        {
            data.Scenes.Add(new ModelSceneData());
        }

        data.Scenes[0].RootNodes.Add(data.Nodes.Count);
        var node = new ModelNodeData { Name = name, MeshIndex = mesh };
        data.Nodes.Add(node);
        return node;
    }
}
