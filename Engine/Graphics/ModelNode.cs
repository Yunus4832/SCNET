using Engine.Core.Collections;
using Engine.Core.Mathematics;

namespace Engine.Graphics;

/// <summary>A node definition in a shared model resource. Per-instance animation belongs to ModelPose.</summary>
public sealed class ModelNode
{
    private readonly List<ModelNode> _children = [];

    internal ModelNode(
        Model model,
        int index,
        int sourceNodeIndex,
        string name,
        Matrix localTransform,
        ModelNode? parent
    )
    {
        Model = model;
        Index = index;
        SourceNodeIndex = sourceNodeIndex;
        Name = name;
        LocalTransform = localTransform;
        Parent = parent;
    }

    public Model Model { get; }

    public int Index { get; }

    public int SourceNodeIndex { get; }

    public string Name { get; }

    public Matrix LocalTransform { get; }

    public ModelNode? Parent { get; }

    public ReadOnlyList<ModelNode> Children => new(_children);

    public ModelMesh? Mesh { get; internal set; }

    public ModelMesh RequireMesh()
    {
        return Mesh ?? throw new InvalidOperationException($"Model node '{Name}' does not reference a mesh.");
    }

    internal void AddChild(ModelNode child)
    {
        _children.Add(child);
    }
}
