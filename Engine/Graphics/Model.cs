using Engine.Core;
using Engine.Media;

namespace Engine.Graphics;

public class Model : IDisposable
{
    private readonly List<ModelBone> _bones = [];

    private readonly List<ModelMesh> _meshes = [];

    public ModelBone RootBone
    {
        get => field is not null ? field : throw new InvalidOperationException("RootBone not initialized");
        private set;
    } = null!;

    public ReadOnlyList<ModelBone> Bones => new(_bones);

    public ReadOnlyList<ModelMesh> Meshes => new(_meshes);

    public void Dispose()
    {
        InternalDispose();
    }

    public ModelBone? FindBone(string name, bool throwIfNotFound = true)
    {
        foreach (var bone in _bones.Where(bone => bone.Name == name))
        {
            return bone;
        }

        return throwIfNotFound ? throw new InvalidOperationException("ModelBone not found.") : null;
    }

    public ModelMesh? FindMesh(string name, bool throwIfNotFound = true)
    {
        foreach (var mesh in _meshes.Where(mesh => mesh.Name == name))
        {
            return mesh;
        }

        return throwIfNotFound ? throw new InvalidOperationException("ModelMesh not found.") : null;
    }

    public ModelBone NewBone(string name, Matrix transform, ModelBone? parentBone)
    {
        if (parentBone == null && _bones.Count > 0)
        {
            throw new InvalidOperationException("There can be only one root bone.");
        }

        if (parentBone != null && parentBone.Model != this)
        {
            throw new InvalidOperationException("Parent bone must belong to the same model.");
        }

        var modelBone = new ModelBone
        {
            Name = name,
            Model = this,
            Index = _bones.Count,
            ParentBone = parentBone,
            Transform = transform,
        };

        _bones.Add(modelBone);

        if (parentBone != null)
        {
            parentBone.ChildBones.Add(modelBone);
        }
        else
        {
            RootBone = modelBone;
        }

        return modelBone;
    }

    public void AddMesh(ModelMesh mesh)
    {
        _meshes.Add(mesh);
    }

    public ModelMesh NewMesh(string name, ModelBone parentBone, BoundingBox boundingBox)
    {
        if (parentBone.Model != this)
        {
            throw new InvalidOperationException("Parent bone must belong to the same model.");
        }

        return new ModelMesh
        {
            Name = name,
            ParentBone = parentBone,
            BoundingBox = boundingBox
        };
    }

    public void CopyAbsoluteBoneTransformsTo(Matrix[] absoluteTransforms)
    {
        if (absoluteTransforms.Length < _bones.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteTransforms));
        }

        for (var i = 0; i < _bones.Count; i++)
        {
            var modelBone = _bones[i];
            if (modelBone.ParentBone is null)
            {
                absoluteTransforms[i] = modelBone.Transform;
            }
            else
            {
                Matrix.MultiplyRestricted(ref modelBone.Transform, ref absoluteTransforms[modelBone.ParentBone.Index],
                    out absoluteTransforms[i]);
            }
        }
    }

    public BoundingBox CalculateAbsoluteBoundingBox(Matrix[] absoluteTransforms)
    {
        if (absoluteTransforms.Length < _bones.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteTransforms));
        }

        var result = default(BoundingBox);
        var flag = false;
        foreach (var mesh in Meshes)
        {
            if (mesh.ParentBone is null)
            {
                continue;
            }

            if (flag)
            {
                BoundingBox.Transform(
                    ref mesh.BoundingBox,
                    ref absoluteTransforms[mesh.ParentBone.Index],
                    out var result2
                );
                result = BoundingBox.Union(result, result2);
            }
            else
            {
                BoundingBox.Transform(ref mesh.BoundingBox, ref absoluteTransforms[mesh.ParentBone.Index],
                    out result);
                flag = true;
            }
        }

        return result;
    }

    public void CopyAbsoluteBoneTransformsTo(Matrix[] absoluteTransforms, Matrix matrix)
    {
        if (absoluteTransforms.Length < _bones.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteTransforms));
        }

        for (var i = 0; i < _bones.Count; i++)
        {
            var modelBone = _bones[i];
            if (modelBone.ParentBone == null)
            {
                Matrix.MultiplyRestricted(ref modelBone.Transform, ref matrix, out absoluteTransforms[i]);
            }
            else
            {
                Matrix.MultiplyRestricted(ref modelBone.Transform, ref absoluteTransforms[modelBone.ParentBone.Index],
                    out absoluteTransforms[i]);
            }
        }
    }

    public static Model Load(ModelData modelData, bool keepSourceVertexDataInTags = false)
    {
        var model = new Model();
        model.Initialize(modelData, keepSourceVertexDataInTags);
        return model;
    }

    public static Model Load(Stream stream, bool keepSourceVertexDataInTags = false)
    {
        return Load(ModelData.Load(stream), keepSourceVertexDataInTags);
    }

    public static Model Load(string fileName, bool keepSourceVertexDataInTags = false)
    {
        return Load(ModelData.Load(fileName), keepSourceVertexDataInTags);
    }

    public void Initialize(ModelData modelData, bool keepSourceVertexDataInTags)
    {
        InternalDispose();
        var array = new VertexBuffer[modelData.Buffers.Count];
        var array2 = new IndexBuffer[modelData.Buffers.Count];
        for (var i = 0; i < modelData.Buffers.Count; i++)
        {
            var modelBuffersData = modelData.Buffers[i];
            array[i] = new VertexBuffer(modelBuffersData.VertexDeclaration,
                modelBuffersData.Vertices.Length / modelBuffersData.VertexDeclaration.VertexStride);
            array[i].SetData(modelBuffersData.Vertices, 0, modelBuffersData.Vertices.Length);
            array2[i] = new IndexBuffer(IndexFormat.SixteenBits, modelBuffersData.Indices.Length / 2);
            array2[i].SetData(modelBuffersData.Indices, 0, modelBuffersData.Indices.Length);
            if (!keepSourceVertexDataInTags)
            {
                continue;
            }

            array[i].Tag = modelBuffersData.Vertices;
            array2[i].Tag = modelBuffersData.Indices;
        }

        foreach (var bone in modelData.Bones)
        {
            NewBone(bone.Name, bone.Transform, bone.ParentBoneIndex >= 0 ? _bones[bone.ParentBoneIndex] : null);
        }

        foreach (var mesh in modelData.Meshes)
        {
            var modelMesh = NewMesh(mesh.Name, _bones[mesh.ParentBoneIndex], mesh.BoundingBox);
            _meshes.Add(modelMesh);
            foreach (var meshPart in mesh.MeshParts)
            {
                modelMesh.NewMeshPart(array[meshPart.BuffersDataIndex], array2[meshPart.BuffersDataIndex],
                    meshPart.StartIndex, meshPart.IndicesCount, meshPart.BoundingBox);
            }
        }
    }

    private void InternalDispose()
    {
        RootBone = null!;
        _bones.Clear();
        Utilities.DisposeCollection(_meshes);
    }
}
