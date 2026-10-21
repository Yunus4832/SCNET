using Engine.Core.Mathematics;

namespace Engine.Graphics;

/// <summary>Placement and procedural animation state for one instance of a shared model.</summary>
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

    /// <summary>Applies the game's rigid animation convention: append to rest rotation/scale while retaining the pivot translation.</summary>
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
