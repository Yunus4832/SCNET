using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCreatureModel : ComponentModel, IUpdateable
{
    public bool LastAttackOrder;

    public bool LastFeedOrder;

    public ComponentCreature ComponentCreature = null!;

    private Vector3? _eyePosition;

    private Quaternion? _eyeRotation;

    private float _injuryColorFactor;

    protected readonly Random random = new();

    private Vector3 _randomLookPoint;

    protected SubsystemGameInfo subsystemGameInfo = null!;

    protected SubsystemTime subsystemTime = null!;

    public float Bob { get; set; }

    public float MovementAnimationPhase { get; set; }

    public float DeathPhase { get; set; }

    public Vector3 DeathCauseOffset { get; set; }

    public float HeadShakeOrder { get; set; }

    public bool RowLeftOrder { get; set; }

    public bool RowRightOrder { get; set; }

    public float AimHandAngleOrder { get; set; }

    public Vector3 InHandItemOffsetOrder { get; set; }

    public Vector3 InHandItemRotationOrder { get; set; }

    public bool IsAttackHitMoment { get; set; }

    public Vector3? LookAtOrder { get; set; }

    public bool LookRandomOrder { get; set; }

    public bool AttackOrder { get; set; }

    public bool FeedOrder { get; set; }


    public Vector3 EyePosition
    {
        get
        {
            _eyePosition ??= CalculateEyePosition();
            return _eyePosition.Value;
        }
    }

    public Quaternion EyeRotation
    {
        get
        {
            _eyeRotation ??= CalculateEyeRotation();
            return _eyeRotation.Value;
        }
    }

    public UpdateOrder UpdateOrder
    {
        get
        {
            var parentBody = ComponentCreature.ComponentBody.ParentBody;
            var componentCreatureModel = parentBody?.Entity.FindComponent<ComponentCreatureModel>();
            if (componentCreatureModel != null)
            {
                return componentCreatureModel.UpdateOrder + 1;
            }

            return UpdateOrder.CreatureModels;
        }
    }

    public virtual void Update(float dt)
    {
        if (LookRandomOrder)
        {
            var matrix = ComponentCreature.ComponentBody.Matrix;
            var v = Vector3.Normalize(_randomLookPoint - ComponentCreature.ComponentCreatureModel.EyePosition);
            if (random.Float(0f, 1f) < 0.25f * dt || Vector3.Dot(matrix.Forward, v) < 0.2f)
            {
                var s = random.Float(-5f, 5f);
                var s2 = random.Float(-1f, 1f);
                var s3 = random.Float(3f, 8f);
                _randomLookPoint = ComponentCreature.ComponentCreatureModel.EyePosition + s3 * matrix.Forward +
                                   s2 * matrix.Up + s * matrix.Right;
            }

            LookAtOrder = _randomLookPoint;
        }

        if (LookAtOrder.HasValue)
        {
            var forward = ComponentCreature.ComponentBody.Matrix.Forward;
            var v2 = LookAtOrder.Value - ComponentCreature.ComponentCreatureModel.EyePosition;
            var x = Vector2.Angle(new Vector2(forward.X, forward.Z), new Vector2(v2.X, v2.Z));
            var y = MathUtils.Asin(0.99f * Vector3.Normalize(v2).Y);
            ComponentCreature.ComponentLocomotion.LookOrder =
                new Vector2(x, y) - ComponentCreature.ComponentLocomotion.LookAngles;
        }

        if (HeadShakeOrder > 0f)
        {
            HeadShakeOrder = MathUtils.Max(HeadShakeOrder - dt, 0f);
            var num = 1f * MathUtils.Saturate(4f * HeadShakeOrder);
            ComponentCreature.ComponentLocomotion.LookOrder =
                new Vector2(num * (float)MathUtils.Sin(16.0 * subsystemTime.GameTime + 0.01f * GetHashCode()), 0f) -
                ComponentCreature.ComponentLocomotion.LookAngles;
        }

        if (ComponentCreature.ComponentHealth.Health == 0f)
        {
            DeathPhase = MathUtils.Min(DeathPhase + 3f * dt, 1f);
        }

        if (ComponentCreature.ComponentHealth.HealthChange < 0f)
        {
            _injuryColorFactor = 1f;
        }

        _injuryColorFactor = MathUtils.Saturate(_injuryColorFactor - 3f * dt);
        _eyePosition = null;
        _eyeRotation = null;
        LookRandomOrder = false;
        LookAtOrder = null;
    }

    public override void Animate()
    {
        Opacity = ComponentCreature.ComponentSpawn.SpawnDuration > 0f
            ? (float)MathUtils.Saturate(
                (subsystemGameInfo.TotalElapsedGameTime - ComponentCreature.ComponentSpawn.SpawnTime) /
                ComponentCreature.ComponentSpawn.SpawnDuration)
            : 1f;
        if (ComponentCreature.ComponentSpawn.DespawnTime.HasValue)
        {
            Opacity = MathUtils.Min(Opacity.Value,
                (float)MathUtils.Saturate(1.0 -
                                          (subsystemGameInfo.TotalElapsedGameTime -
                                           ComponentCreature.ComponentSpawn.DespawnTime.Value) /
                                          ComponentCreature.ComponentSpawn.DespawnDuration));
        }

        DiffuseColor = Vector3.Lerp(Vector3.One, new Vector3(1f, 0f, 0f), _injuryColorFactor);
        if (Opacity.HasValue && Opacity.Value < 1f)
        {
            var num = ComponentCreature.ComponentBody.ImmersionFactor >= 1f;
            var flag = subsystemSky.ViewUnderWaterDepth > 0f;
            RenderingMode = num == flag
                ? ModelRenderingMode.TransparentAfterWater
                : ModelRenderingMode.TransparentBeforeWater;
        }
        else
        {
            RenderingMode = ModelRenderingMode.Solid;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        ComponentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        ComponentCreature.ComponentHealth.Attacked += delegate(ComponentCreature attacker)
        {
            if (DeathPhase == 0f && ComponentCreature.ComponentHealth.Health == 0f)
            {
                DeathCauseOffset = attacker.ComponentBody.BoundingBox.Center() -
                                   ComponentCreature.ComponentBody.BoundingBox.Center();
            }
        };
    }

    public override void OnEntityAdded()
    {
        ComponentCreature.ComponentBody.PositionChanged += delegate { _eyePosition = null; };
        ComponentCreature.ComponentBody.RotationChanged += delegate { _eyeRotation = null; };
    }

    public virtual Vector3 CalculateEyePosition()
    {
        var matrix = ComponentCreature.ComponentBody.Matrix;
        return ComponentCreature.ComponentBody.Position +
               matrix.Up * 0.95f * ComponentCreature.ComponentBody.BoxSize.Y +
               matrix.Forward * 0.45f * ComponentCreature.ComponentBody.BoxSize.Z;
    }

    public virtual Quaternion CalculateEyeRotation()
    {
        return ComponentCreature.ComponentBody.Rotation * Quaternion.CreateFromYawPitchRoll(
            0f - ComponentCreature.ComponentLocomotion.LookAngles.X,
            ComponentCreature.ComponentLocomotion.LookAngles.Y, 0f);
    }
}
