using Engine.Core.Mathematics;

namespace Engine.Media.Models;

public sealed class ModelNodeData : ModelElementData
{
    public Matrix? MatrixTransform;

    public Vector3 Translation;

    public Quaternion Rotation = Quaternion.Identity;

    public Vector3 Scale = Vector3.One;

    /// <summary>Authored TRS is retained separately: decomposing a matrix can change animated negative-scale semantics.</summary>
    public Matrix Transform => MatrixTransform ??
                               Matrix.CreateScale(Scale) *
                               Matrix.CreateFromQuaternion(Rotation) *
                               Matrix.CreateTranslation(Translation);

    public readonly List<int> Children = [];

    public int MeshIndex = -1;

    public int SkinIndex = -1;

    public int CameraIndex = -1;

    /// <summary>Null inherits the mesh's default morph weights.</summary>
    public float[]? MorphWeights;
}
