using Engine.Graphics;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBirdModel : ComponentCreatureModel
{
    private float _flyAnimationSpeed;

    private bool _hasWings;

    private float _peckAnimationSpeed;

    private float _peckPhase;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private ModelBone _bodyBone = null!;

    private ModelBone _headBone = null!;

    private ModelBone _leg1Bone = null!;

    private ModelBone _leg2Bone = null!;

    private ModelBone _neckBone = null!;

    private ModelBone? _wing1Bone;

    private ModelBone? _wing2Bone;

    public float FlyPhase { get; set; }

    public override void Update(float dt)
    {
        var num = Vector3.Dot(ComponentCreature.ComponentBody.Velocity,
            ComponentCreature.ComponentBody.Matrix.Forward);
        if (MathUtils.Abs(num) > 0.1f)
        {
            MovementAnimationPhase += num * dt * _walkAnimationSpeed;
        }
        else
        {
            var num2 = MathUtils.Floor(MovementAnimationPhase);
            if (MovementAnimationPhase.UncloseTo(num2))
            {
                MovementAnimationPhase = MovementAnimationPhase - num2 > 0.5f
                    ? MathUtils.Min(MovementAnimationPhase + 2f * dt, num2 + 1f)
                    : MathUtils.Max(MovementAnimationPhase - 2f * dt, num2);
            }
        }

        var num3 = (0f - _walkBobHeight) * MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        var num4 = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += num4 * (num3 - Bob);
        if (_hasWings)
        {
            if (ComponentCreature.ComponentLocomotion.LastFlyOrder.HasValue)
            {
                var num5 = ComponentCreature.ComponentLocomotion.LastFlyOrder.Value.LengthSquared() > 0.99f
                    ? 1.5f
                    : 1f;
                FlyPhase = MathUtils.Remainder(FlyPhase + _flyAnimationSpeed * num5 * dt, 1f);
                if (ComponentCreature.ComponentLocomotion.LastFlyOrder.Value.Y < -0.1f &&
                    ComponentCreature.ComponentBody.Velocity.Length() > 4f)
                {
                    FlyPhase = 0.72f;
                }
            }
            else if (FlyPhase.UncloseTo(1f))
            {
                FlyPhase = MathUtils.Min(FlyPhase + _flyAnimationSpeed * dt, 1f);
            }
        }

        if (FeedOrder)
        {
            _peckPhase += _peckAnimationSpeed * dt;
            if (_peckPhase > 0.75f)
            {
                _peckPhase -= 0.5f;
            }
        }
        else if (_peckPhase != 0f)
        {
            _peckPhase = MathUtils.Remainder(MathUtils.Min(_peckPhase + _peckAnimationSpeed * dt, 1f), 1f);
        }

        FeedOrder = false;
        base.Update(dt);
    }

    public override void Animate()
    {
        var flag = false;
        ModsManager.HookAction("OnModelAnimate", loader =>
        {
            loader.OnModelAnimate(this, out var skip);
            flag |= skip;
            return false;
        });
        if (flag)
        {
            base.Animate();
            return;
        }

        var num = 0f;
        if (_hasWings)
        {
            num += 1.2f * MathUtils.Sin((float)Math.PI * 2f * (FlyPhase + 0.75f));
            if (ComponentCreature.ComponentBody.StandingOnValue.HasValue)
            {
                num += 0.3f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            }
        }

        float num2;
        float num3;
        if (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
            ComponentCreature.ComponentBody.ImmersionFactor > 0f ||
            ComponentCreature.ComponentLocomotion.FlySpeed == 0f)
        {
            num2 = 0.6f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            num3 = 0f - num2;
        }
        else
        {
            num2 = num3 = 0f - MathUtils.DegToRad(60f);
        }

        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var yaw = ComponentCreature.ComponentLocomotion.LookAngles.X / 2f;
            var yaw2 = ComponentCreature.ComponentLocomotion.LookAngles.X / 2f;
            var num4 = 0f;
            var num5 = 0f;
            if (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
                ComponentCreature.ComponentBody.ImmersionFactor > 0f)
            {
                num4 = 0.5f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase / 2f);
                num5 = 0f - num4;
            }

            var num6 = MathUtils.Cos((float)Math.PI * 2f * _peckPhase);
            num4 -= 1.25f * (1f - (num6 >= 0f ? num6 : -0.5f * num6));
            num4 += ComponentCreature.ComponentLocomotion.LookAngles.Y;
            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) *
                Matrix.CreateTranslation(ComponentCreature.ComponentBody.Position + new Vector3(0f, Bob, 0f)));
            SetBoneTransform(_neckBone.Index, Matrix.CreateFromYawPitchRoll(yaw2, num4, 0f));
            SetBoneTransform(_headBone.Index,
                Matrix.CreateFromYawPitchRoll(yaw,
                    num5 + MathUtils.Clamp(vector.Y, -(float)Math.PI / 4f, (float)Math.PI / 4f), vector.Z));
            if (_hasWings)
            {
                SetBoneTransform(_wing1Bone!.Index, Matrix.CreateRotationY(num));
                SetBoneTransform(_wing2Bone!.Index, Matrix.CreateRotationY(0f - num));
            }

            SetBoneTransform(_leg1Bone.Index, Matrix.CreateRotationX(num2));
            SetBoneTransform(_leg2Bone.Index, Matrix.CreateRotationX(num3));
        }
        else
        {
            var num7 = 1f - DeathPhase;
            var num8 = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                       ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            var position = ComponentCreature.ComponentBody.Position + 0.5f * num8 *
                Vector3.Normalize(ComponentCreature.ComponentBody.Matrix.Forward * new Vector3(1f, 0f, 1f));
            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, (float)Math.PI / 2f * DeathPhase, 0f) *
                Matrix.CreateTranslation(position));
            SetBoneTransform(_neckBone.Index, Matrix.Identity);
            SetBoneTransform(_headBone.Index, Matrix.Identity);
            if (_hasWings)
            {
                SetBoneTransform(_wing1Bone!.Index, Matrix.CreateRotationY(num * num7));
                SetBoneTransform(_wing2Bone!.Index, Matrix.CreateRotationY((0f - num) * num7));
            }

            SetBoneTransform(_leg1Bone.Index, Matrix.CreateRotationX(num2 * num7));
            SetBoneTransform(_leg2Bone.Index, Matrix.CreateRotationX(num3 * num7));
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _flyAnimationSpeed = valuesDictionary.GetValue<float>("FlyAnimationSpeed");
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _peckAnimationSpeed = valuesDictionary.GetValue<float>("PeckAnimationSpeed");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyBone = Model.FindBone("Body")!;
        _neckBone = Model.FindBone("Neck")!;
        _headBone = Model.FindBone("Head")!;
        _leg1Bone = Model.FindBone("Leg1")!;
        _leg2Bone = Model.FindBone("Leg2")!;
        _wing1Bone = Model.FindBone("Wing1", false);
        _wing2Bone = Model.FindBone("Wing2", false);
        _hasWings = _wing1Bone != null && _wing2Bone != null;
    }
}
