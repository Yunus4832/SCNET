using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentModel : Component
{
    private bool _isSet;

    private Matrix?[] _boneTransforms = [];

    private float _boundingSphereRadius;

    protected ComponentFrame componentFrame = null!;

    private Model _model = null!;

    protected SubsystemSky subsystemSky = null!;

    public float? Opacity { get; set; }

    public Vector3? DiffuseColor { get; set; }

    public Vector4? EmissionColor { get; set; }

    public Model Model
    {
        get => _model;
        private set => SetModel(value);
    }

    public Texture2D? TextureOverride { get; set; }

    public bool CastsShadow { get; set; }

    public int PrepareOrder { get; set; }

    public virtual ModelRenderingMode RenderingMode { get; set; }

    public int[] MeshDrawOrders { get; set; } = [];

    public bool IsVisibleForCamera { get; set; }

    public Matrix[] AbsoluteBoneTransformsForCamera { get; set; } = [];

    public Matrix? GetBoneTransform(int boneIndex)
    {
        return _boneTransforms[boneIndex];
    }

    public void SetBoneTransform(int boneIndex, Matrix? transformation)
    {
        _boneTransforms[boneIndex] = transformation;
    }

    public void CalculateAbsoluteBonesTransforms(Camera camera)
    {
        if (_model == null)
        {
            return;
        }

        ProcessBoneHierarchy(
            Model.RootBone ?? throw new InvalidOperationException("Required Model.RootBone is null"),
            camera.ViewMatrix,
            AbsoluteBoneTransformsForCamera
        );
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
#if SERVER
        CastsShadow = false;
        TextureOverride = null;
        PrepareOrder = valuesDictionary.GetValue<int>("PrepareOrder");
        _boundingSphereRadius = valuesDictionary.GetValue<float>("BoundingSphereRadius");
#else
        var value = valuesDictionary.GetValue<string>("ModelName");
        Model = ContentManager.Get<Model>(value);
        CastsShadow = valuesDictionary.GetValue<bool>("CastsShadow");
        var value2 = valuesDictionary.GetValue<string>("TextureOverride");
        TextureOverride = string.IsNullOrEmpty(value2) ? null : ContentManager.Get<Texture2D>(value2);
        PrepareOrder = valuesDictionary.GetValue<int>("PrepareOrder");
        _boundingSphereRadius = valuesDictionary.GetValue<float>("BoundingSphereRadius");
#endif
    }

    public virtual void SetModel(Model model)
    {
        _isSet = false;
        ModsManager.HookAction("OnSetModel", modLoader =>
        {
            modLoader.OnSetModel(this, model, out _isSet);
            return _isSet;
        });
        if (_isSet)
        {
            return;
        }

        _model = model;
        _boneTransforms = new Matrix?[_model.Bones.Count];
        AbsoluteBoneTransformsForCamera = new Matrix[_model.Bones.Count];
        MeshDrawOrders = Enumerable.Range(0, _model.Meshes.Count).ToArray();
    }

    public void ProcessBoneHierarchy(ModelBone modelBone, Matrix currentTransform, Matrix[] transforms)
    {
        var m = modelBone.Transform;
        if (_boneTransforms[modelBone.Index].HasValue)
        {
            var translation = m.Translation;
            m.Translation = Vector3.Zero;
            m *= _boneTransforms[modelBone.Index]!.Value;
            m.Translation += translation;
        }

        Matrix.MultiplyRestricted(ref m, ref currentTransform, out transforms[modelBone.Index]);

        foreach (var childBone in modelBone.ReadOnlyChildBones)
        {
            ProcessBoneHierarchy(childBone, transforms[modelBone.Index], transforms);
        }
    }
}
