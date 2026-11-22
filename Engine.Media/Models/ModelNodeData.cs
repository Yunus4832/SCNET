using Engine.Core.Mathematics;

namespace Engine.Media.Models;

public sealed class ModelNodeData : ModelElementData
{
    public Matrix? MatrixTransform;

    public Vector3 Translation;

    public Quaternion Rotation = Quaternion.Identity;

    public Vector3 Scale = Vector3.One;

    /// <summary>单独保留创作时的平移、旋转和缩放，避免矩阵分解改变负缩放动画的语义。</summary>
    public Matrix Transform => MatrixTransform ??
                               Matrix.CreateScale(Scale) *
                               Matrix.CreateFromQuaternion(Rotation) *
                               Matrix.CreateTranslation(Translation);

    public readonly List<int> Children = [];

    public int MeshIndex = -1;

    public int SkinIndex = -1;

    public int CameraIndex = -1;

    /// <summary>为空时继承网格的默认形变权重。</summary>
    public float[]? MorphWeights;
}
