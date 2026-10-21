using Engine.Core.Collections;
using Engine.Core.Mathematics;
using Engine.FileStorage;
using Engine.Media.Models;

namespace Engine.Graphics;

/// <summary>A shared, static scene resource. Nodes reference mesh resources; poses own instance state.</summary>
public sealed class Model : IDisposable
{
    private readonly List<ModelNode> _nodes = [];

    private readonly List<ModelNode> _roots = [];

    private readonly List<ModelNode> _meshNodes = [];

    private readonly List<ModelMesh> _meshes = [];

    private Model(ModelData data, bool keepSource)
    {
        SourceData = data;
        if (data.Scenes.Count == 0)
        {
            throw new InvalidDataException("Model has no renderable scene.");
        }

        SceneIndex = data.DefaultSceneIndex < 0 ? 0 : data.DefaultSceneIndex;
        var scene = data.Scenes[SceneIndex];
        if (scene.RootNodes.Count == 0)
        {
            throw new InvalidDataException("Selected model scene has no nodes to render.");
        }

        try
        {
            var visited = new HashSet<int>();
            foreach (var root in scene.RootNodes)
            {
                _roots.Add(InitializeNode(root, null, keepSource, visited));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public ModelData SourceData { get; }

    public int SceneIndex { get; }

    public bool IsDisposed { get; private set; }

    public ReadOnlyList<ModelNode> Nodes => new(_nodes);

    public ReadOnlyList<ModelNode> RootNodes => new(_roots);

    /// <summary>Renderable nodes in default draw order; a mesh may occur on more than one node.</summary>
    public ReadOnlyList<ModelNode> MeshNodes => new(_meshNodes);

    /// <summary>Unique prepared mesh resources, including distinct default morph-weight variants.</summary>
    public ReadOnlyList<ModelMesh> Meshes => new(_meshes);

    public event Action<Model>? Disposed;

    public ModelNode? FindNode(string name, bool throwIfNotFound = true)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var matches = _nodes.Where(n => n.Name == name).Take(2).ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Model node name '{name}' is ambiguous; use SourceNodeIndex to identify the node."
            );
        }

        return matches.Length == 1 ? matches[0] :
            throwIfNotFound ? throw new InvalidOperationException($"Model node '{name}' was not found.") : null;
    }

    public void CopyAbsoluteNodeTransformsTo(Matrix[] transforms)
    {
        CopyAbsoluteNodeTransformsTo(transforms, Matrix.Identity);
    }

    public void CopyAbsoluteNodeTransformsTo(Matrix[] transforms, Matrix worldTransform)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (transforms.Length < _nodes.Count)
        {
            throw new ArgumentException("Transform array is smaller than the node count.", nameof(transforms));
        }

        foreach (var node in _nodes)
        {
            transforms[node.Index] = node.LocalTransform *
                                     (node.Parent == null ? worldTransform : transforms[node.Parent.Index]);
        }
    }

    public BoundingBox CalculateAbsoluteBoundingBox(Matrix[] transforms)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var bounds = default(BoundingBox);
        var first = true;
        foreach (var node in _meshNodes)
        {
            var local = node.RequireMesh().BoundingBox;
            BoundingBox.Transform(ref local, ref transforms[node.Index], out var box);
            bounds = first ? box : BoundingBox.Union(bounds, box);
            first = false;
        }

        return bounds;
    }

    public static Model Load(ModelData data, bool keepSourceVertexDataInTags = false)
    {
        return new Model(data, keepSourceVertexDataInTags);
    }

    public static Model Load(Stream stream, bool keepSourceVertexDataInTags = false)
    {
        return Load(ModelData.Load(stream), keepSourceVertexDataInTags);
    }

    public static Model Load(string fileName, bool keepSourceVertexDataInTags = false)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(
            ModelData.Load(
                stream,
                uri => Storage.OpenFile(
                    Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, uri),
                    OpenFileMode.Read
                )
            ),
            keepSourceVertexDataInTags
        );
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        try
        {
            Disposed?.Invoke(this);
        }
        finally
        {
            Disposed = null;
            foreach (var mesh in _meshes)
            {
                mesh.ReleaseBuffers();
            }

            _meshes.Clear();
        }
    }

    private ModelNode InitializeNode(int index, ModelNode? parent, bool keepSource, HashSet<int> visited)
    {
        if (!visited.Add(index))
        {
            throw new InvalidDataException("A scene node cannot have multiple parents or form a cycle.");
        }

        var data = SourceData.Nodes[index];
        if (data.SkinIndex >= 0)
        {
            throw new NotSupportedException(
                $"Node '{data.Name}' requires skinned rendering; the rigid Model renderer cannot evaluate skins."
            );
        }

        var node = new ModelNode(this, _nodes.Count, index, data.Name, data.Transform, parent);
        _nodes.Add(node);
        foreach (var child in data.Children)
        {
            node.AddChild(InitializeNode(child, node, keepSource, visited));
        }

        if (data.MeshIndex >= 0)
        {
            var mesh = SourceData.Meshes[data.MeshIndex];
            var targets = mesh.MeshParts.Count == 0 ? 0 : mesh.MeshParts[0].MorphTargets.Count;
            var sourceWeights = data.MorphWeights ?? mesh.MorphWeights;
            if (sourceWeights.Length > targets || mesh.MeshParts.Any(p => p.MorphTargets.Count != targets))
            {
                throw new InvalidDataException("Mesh morph target and default weight counts are inconsistent.");
            }

            var weights = new float[targets];
            sourceWeights.CopyTo(weights, 0);
            node.Mesh = _meshes.FirstOrDefault(m =>
                m.SourceMeshIndex == data.MeshIndex && m.MorphWeights.SequenceEqual(weights)
            );
            if (node.Mesh == null)
            {
                node.Mesh = new ModelMesh(SourceData, data.MeshIndex, weights, keepSource);
                _meshes.Add(node.Mesh);
            }

            _meshNodes.Add(node);
        }

        return node;
    }
}
