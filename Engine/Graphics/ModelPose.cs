using Engine.Core.Mathematics;

namespace Engine.Graphics;

/// <summary>管理共享模型单个实例的放置和程序化动画状态。</summary>
public sealed class ModelPose
{
    private readonly Matrix?[] _animationTransforms;

    public ModelPose(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ObjectDisposedException.ThrowIf(model.IsDisposed, model);
        Model = model;
        _animationTransforms = new Matrix?[model.Nodes.Count];
        AbsoluteTransforms = new Matrix[model.Nodes.Count];
    }

    public Model Model { get; }

    public Matrix WorldTransform { get; set; } = Matrix.Identity;

    public Matrix[] AbsoluteTransforms { get; }

    public Matrix? GetAnimationTransform(int nodeIndex)
    {
        return _animationTransforms[nodeIndex];
    }

    /// <summary>按游戏的刚性动画约定叠加静止姿态的旋转与缩放，保留枢轴平移。</summary>
    public void SetAnimationTransform(int nodeIndex, Matrix? transform)
    {
        _animationTransforms[nodeIndex] = transform;
    }

    public void CalculateAbsoluteTransforms(Matrix viewTransform)
    {
        ObjectDisposedException.ThrowIf(Model.IsDisposed, Model);
        var worldView = WorldTransform * viewTransform;
        foreach (var node in Model.Nodes)
        {
            var local = node.LocalTransform;
            if (_animationTransforms[node.Index] is { } animation)
            {
                var translation = local.Translation;
                local.Translation = Vector3.Zero;
                local *= animation;
                local.Translation += translation;
            }

            AbsoluteTransforms[node.Index] =
                local * (node.Parent == null ? worldView : AbsoluteTransforms[node.Parent.Index]);
        }
    }
}
