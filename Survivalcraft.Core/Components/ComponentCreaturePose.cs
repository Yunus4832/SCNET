using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

/// <summary>Gameplay pose and action timing; independent of model, camera and graphics resources.</summary>
public class ComponentCreaturePose : Component, IUpdateable
{
    private ComponentCreature _creature = null!;

    private SubsystemTime _time = null!;

    private readonly Random _random = new();

    private Vector3 _randomLookPoint;

    private float _lieDown;

    private float _footstepPhase;

    private float _footstepRate;

    public CreaturePoseKind Kind { get; private set; }

    public Vector3? LookAtOrder { get; set; }

    public bool LookRandomOrder { get; set; }

    public float HeadShakeOrder { get; set; }

    public bool AttackOrder { get; set; }

    public bool FeedOrder { get; set; }

    public bool LastAttackOrder { get; private set; }

    public bool LastFeedOrder { get; private set; }

    public float AttackPhase { get; private set; }

    public int AttackCounter { get; private set; }

    public bool IsAttackHitMoment { get; private set; }

    public float? BendOrder { get; set; }

    public float DigInOrder { get; set; }

    public float? LastBendOrder { get; private set; }

    public float LastDigInOrder { get; private set; }

    public bool RowLeftOrder { get; set; }

    public bool RowRightOrder { get; set; }

    public float AimHandAngleOrder { get; set; }

    public Vector3 InHandItemOffsetOrder { get; set; }

    public Vector3 InHandItemRotationOrder { get; set; }

    public Vector3 EyePosition
    {
        get
        {
            var body = _creature.ComponentBody;
            var matrix = body.Matrix;
            if (Kind == CreaturePoseKind.Human)
            {
                var crouch = MathUtils.Sigmoid(body.CrouchFactor, 4);
                var height = .875f * body.BoxSize.Y;
                var eyeHeight = MathUtils.Lerp(
                    MathUtils.Lerp(height, .45f * height, crouch),
                    .2f * height,
                    MathUtils.Sigmoid(_lieDown, 1)
                );
                return body.Position + matrix.Up * eyeHeight - matrix.Forward * .2f * crouch;
            }

            return body.Position +
                   matrix.Up * (Kind == CreaturePoseKind.Fish ? 1f : .95f) * body.BoxSize.Y +
                   matrix.Forward * .45f * body.BoxSize.Z;
        }
    }

    public Quaternion EyeRotation
    {
        get
        {
            var roll = Kind == CreaturePoseKind.Human
                ? MathUtils.DegToRad(80) * MathUtils.Sigmoid(MathUtils.Max(_lieDown - .2f, 0) / .8f, 4)
                : 0;
            return _creature.ComponentBody.Rotation *
                   Quaternion.CreateFromYawPitchRoll(
                       -_creature.ComponentLocomotion.LookAngles.X,
                       _creature.ComponentLocomotion.LookAngles.Y,
                       roll
                   );
        }
    }

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public UpdateOrder UpdateOrder =>
        _creature.ComponentBody.ParentBody?.Entity.FindComponent<ComponentCreaturePose>() is { } parent
            ? parent.UpdateOrder + 1
            : UpdateOrder.CreatureModels;

    public void Update(float dt)
    {
        _lieDown = _creature.ComponentHealth.Health <= 0 ||
                   Entity.FindComponent<ComponentSleep>() is { IsSleeping: true }
            ? MathUtils.Min(_lieDown + dt, 1)
            : MathUtils.Max(_lieDown - dt, 0);
        var authority = WorldRuntimeContext.For(Project).IsAuthority;
        if (authority)
        {
            if (LookRandomOrder)
            {
                var matrix = _creature.ComponentBody.Matrix;
                var direction = Vector3.Normalize(_randomLookPoint - EyePosition);
                if (_random.Float(0, 1) < .25f * dt || Vector3.Dot(matrix.Forward, direction) < .2f)
                {
                    _randomLookPoint = EyePosition +
                                       _random.Float(3, 8) * matrix.Forward +
                                       _random.Float(-1, 1) * matrix.Up +
                                       _random.Float(-5, 5) * matrix.Right;
                }

                LookAtOrder = _randomLookPoint;
            }

            if (LookAtOrder is { } target)
            {
                var forward = _creature.ComponentBody.Matrix.Forward;
                var direction = target - EyePosition;
                _creature.ComponentLocomotion.LookOrder = new Vector2(
                                                              Vector2.Angle(forward.XZ, direction.XZ),
                                                              MathUtils.Asin(.99f * Vector3.Normalize(direction).Y)
                                                          ) -
                                                          _creature.ComponentLocomotion.LookAngles;
            }

            LastAttackOrder = AttackOrder && _creature.ComponentHealth.Health > 0;
            LastFeedOrder = FeedOrder && _creature.ComponentHealth.Health > 0;
            LastBendOrder = BendOrder;
            LastDigInOrder = DigInOrder;
            if (Kind == CreaturePoseKind.Human && !_creature.ComponentBody.IsSneaking)
            {
                var body = _creature.ComponentBody;
                var speed = _creature.ComponentLocomotion.LadderValue.HasValue
                    ? 1.5f * body.Velocity.Length()
                    : (body.Velocity.XZ - body.StandingOnVelocity.XZ).Length();
                if (!_creature.ComponentLocomotion.IsCreativeFlyEnabled && speed > .5f)
                {
                    var before = _footstepPhase;
                    _footstepPhase += speed * _footstepRate * dt;
                    if (MathUtils.Floor(_footstepPhase) > MathUtils.Floor(before))
                    {
                        Project.FindSubsystem<SubsystemNoise>(true)!.MakeNoise(body, .25f, 8f);
                    }
                }
                else { _footstepPhase = 0; }
            }
        }

        if (HeadShakeOrder > 0)
        {
            HeadShakeOrder = MathUtils.Max(HeadShakeOrder - dt, 0);
            _creature.ComponentLocomotion.LookOrder = new Vector2(
                                                          MathUtils.Saturate(4 * HeadShakeOrder) *
                                                          (float)MathUtils.Sin(
                                                              16 * _time.GameTime + .01 * Entity.EntityId
                                                          ),
                                                          0
                                                      ) -
                                                      _creature.ComponentLocomotion.LookAngles;
        }

        AdvanceAttack(dt, authority);
        AttackOrder = FeedOrder = LookRandomOrder = false;
        LookAtOrder = null;
        BendOrder = null;
        DigInOrder = 0;
    }

    private void AdvanceAttack(float dt, bool authority)
    {
        IsAttackHitMoment = false;
        var speed = Kind == CreaturePoseKind.Fish ? _creature.ComponentLocomotion.TurnSpeed : 2;
        var active = LastAttackOrder || Kind == CreaturePoseKind.Fish && LastFeedOrder;
        var previous = AttackPhase;
        if (Kind is CreaturePoseKind.Generic or CreaturePoseKind.Bird) { return; }

        if (active)
        {
            AttackPhase = MathUtils.Remainder(previous + speed * dt, 1);
            if (previous < .5f && AttackPhase >= .5f)
            {
                if (LastAttackOrder) { AttackCounter++; }

                IsAttackHitMoment = authority && LastAttackOrder;
            }
        }
        else if (Kind == CreaturePoseKind.Fish || previous > .5f)
        {
            AttackPhase = MathUtils.Remainder(MathUtils.Min(previous + speed * dt, 1), 1);
        }
        else
        {
            AttackPhase = MathUtils.Max(previous - dt * (Kind == CreaturePoseKind.Human ? previous : speed), 0);
        }
    }

    public void ApplyActionState(bool attack, bool feed, float phase, int counter, float? bend, float digIn)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { return; }

        LastAttackOrder = attack;
        LastFeedOrder = feed;
        AttackPhase = phase;
        AttackCounter = counter;
        LastBendOrder = bend;
        LastDigInOrder = digIn;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _creature = Entity.FindComponent<ComponentCreature>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        Kind = valuesDictionary.GetValue("Kind", CreaturePoseKind.Generic);
        _footstepRate = valuesDictionary.GetValue("FootstepRate", .48f);
        LastAttackOrder = valuesDictionary.GetValue("Attacking", false);
        LastFeedOrder = valuesDictionary.GetValue("Feeding", false);
        AttackPhase = valuesDictionary.GetValue("AttackPhase", 0f);
        AttackCounter = valuesDictionary.GetValue("AttackCounter", 0);
        LastBendOrder = valuesDictionary.GetValue("HasBend", false) ? valuesDictionary.GetValue("Bend", 0f) : null;
        LastDigInOrder = valuesDictionary.GetValue("DigIn", 0f);
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Attacking", LastAttackOrder);
        valuesDictionary.SetValue("Feeding", LastFeedOrder);
        valuesDictionary.SetValue("AttackPhase", AttackPhase);
        valuesDictionary.SetValue("AttackCounter", AttackCounter);
        valuesDictionary.SetValue("HasBend", LastBendOrder.HasValue);
        valuesDictionary.SetValue("Bend", LastBendOrder ?? 0f);
        valuesDictionary.SetValue("DigIn", LastDigInOrder);
    }
}
