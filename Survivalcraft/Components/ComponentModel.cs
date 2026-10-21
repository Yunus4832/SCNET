using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentModel : Component
{
    private ModelPose? _pose;

    private float _boundingSphereRadius;

    protected ComponentFrame componentFrame = null!;

    private Model _model = null!;

    protected SubsystemSky subsystemSky = null!;

    public float? Opacity { get; set; }

    public Vector3? DiffuseColor { get; set; }

    public Vector4? EmissionColor { get; set; }

    public Model Model { get => _model; private set => SetModel(value); }

    public Texture2D? TextureOverride { get; set; }

    public bool CastsShadow { get; set; }

    public int PrepareOrder { get; set; }

    public virtual ModelRenderingMode RenderingMode { get; set; }

    public int[] DrawNodeIndices { get; set; } = [];

    public bool IsVisibleForCamera { get; set; }

    public ModelPose Pose => _pose ?? throw new InvalidOperationException("Model pose is not initialized.");

    public Matrix[] AbsoluteNodeTransformsForCamera => _pose?.AbsoluteTransforms ?? [];

    public Matrix? GetNodeTransform(int nodeIndex)
    {
        return Pose.GetAnimationTransform(nodeIndex);
    }

    public void SetNodeTransform(int nodeIndex, Matrix? transformation)
    {
        Pose.SetAnimationTransform(nodeIndex, transformation);
    }

    public void CalculateAbsoluteNodeTransforms(Camera camera)
    {
        _pose?.CalculateAbsoluteTransforms(camera.ViewMatrix);
    }

    public virtual void CalculateIsVisible(Camera camera)
    {
        if (camera.GameWidget.IsEntityFirstPersonTarget(Entity))
        {
            IsVisibleForCamera = false;
            return;
        }

        var num = MathUtils.Sqr(subsystemSky.VisibilityRange);
        var vector = componentFrame.Position - camera.ViewPosition;
        vector.Y *= subsystemSky.VisibilityRangeYMultiplier;
        if (vector.LengthSquared() < num)
        {
            var sphere = new BoundingSphere(componentFrame.Position, _boundingSphereRadius);
            IsVisibleForCamera = camera.ViewFrustum.Intersection(sphere);
        }
        else
        {
            IsVisibleForCamera = false;
        }
    }

    public virtual void Animate()
    {
    }

    public virtual void DrawExtras(Camera camera)
    {
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        componentFrame = Entity.FindComponent<ComponentFrame>(true)!;
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            CastsShadow = false;
            TextureOverride = null;
        }
        else
        {
            var value = valuesDictionary.GetValue<string>("ModelName");
            Model = ContentManager.Get<Model>(value);
            CastsShadow = valuesDictionary.GetValue<bool>("CastsShadow");
            var value2 = valuesDictionary.GetValue<string>("TextureOverride");
            TextureOverride = string.IsNullOrEmpty(value2) ? null : ContentManager.Get<Texture2D>(value2);
        }

        PrepareOrder = valuesDictionary.GetValue<int>("PrepareOrder");
        _boundingSphereRadius = valuesDictionary.GetValue<float>("BoundingSphereRadius");
    }

    public virtual void SetModel(Model model)
    {
        _model = model;
        _pose = new ModelPose(model);
        DrawNodeIndices = model.MeshNodes.Select(n => n.Index).ToArray();
    }
}
