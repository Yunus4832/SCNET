using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFlightlessBirdModel : ComponentCreatureModel
{
    private ModelBone _bodyBone = null!;

    private float _feedFactor;

    private float _footstepsPhase;

    private float _headAngleY;

    private ModelBone _headBone = null!;

    private float _kickFactor;

    private float _kickPhase;

    private ModelBone _leg1Bone = null!;

    private ModelBone _leg2Bone = null!;

    private float _legAngle1;

    private float _legAngle2;

    private ModelBone? _neckBone;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private float _walkLegsAngle;

    public override void Update(float dt)
    {
        var footstepsPhase = _footstepsPhase;
        var num = ComponentCreature.ComponentLocomotion.SlipSpeed ?? Vector3.Dot(
            ComponentCreature.ComponentBody.Velocity, ComponentCreature.ComponentBody.Matrix.Forward);
        if (MathUtils.Abs(num) > 0.2f)
        {
            MovementAnimationPhase += num * dt * _walkAnimationSpeed;
            _footstepsPhase += 1.25f * _walkAnimationSpeed * num * dt;
        }
        else
        {
            MovementAnimationPhase = 0f;
            _footstepsPhase = 0f;
        }

        var num2 = (0f - _walkBobHeight) * MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        var num3 = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += num3 * (num2 - Bob);
        var num4 = MathUtils.Floor(_footstepsPhase);
        if (_footstepsPhase > num4 && footstepsPhase <= num4)
        {
            ComponentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
        }

        _feedFactor =
            FeedOrder ? MathUtils.Min(_feedFactor + 2f * dt, 1f) : MathUtils.Max(_feedFactor - 2f * dt, 0f);
        IsAttackHitMoment = false;
        if (AttackOrder)
        {
            _kickFactor = MathUtils.Min(_kickFactor + 6f * dt, 1f);
            var kickPhase = _kickPhase;
            _kickPhase = MathUtils.Remainder(_kickPhase + dt * 2f, 1f);
            if (kickPhase < 0.5f && _kickPhase >= 0.5f)
            {
                IsAttackHitMoment = true;
            }
        }
        else
        {
            _kickFactor = MathUtils.Max(_kickFactor - 6f * dt, 0f);
            if (_kickPhase != 0f)
            {
                _kickPhase = _kickPhase switch
                {
                    > 0.5f => MathUtils.Remainder(MathUtils.Min(_kickPhase + dt * 2f, 1f), 1f),
                    > 0f => MathUtils.Max(_kickPhase - dt * 2f, 0f),
                    _ => _kickPhase
                };
            }
        }

        LastAttackOrder = AttackOrder;
        LastFeedOrder = FeedOrder;
        AttackOrder = false;
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

        var position = ComponentCreature.ComponentBody.Position;
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var num = 0f;
            var num2 = 0f;
            var num3 = 0f;
            if (MovementAnimationPhase != 0f && (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
                                                 ComponentCreature.ComponentBody.ImmersionFactor > 0f))
            {
                var num4 =
                    Vector3.Dot(ComponentCreature.ComponentBody.Velocity,
                        ComponentCreature.ComponentBody.Matrix.Forward) >
                    0.75f * ComponentCreature.ComponentLocomotion.WalkSpeed
                        ? 1.5f * _walkLegsAngle
                        : _walkLegsAngle;
                var num5 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                var num6 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                num = num4 * num5 + _kickPhase;
                num2 = num4 * num6;
                num3 = MathUtils.DegToRad(5f) * MathUtils.Sin((float)Math.PI * 4f * MovementAnimationPhase);
            }

            if (_kickFactor != 0f)
            {
                var x = MathUtils.DegToRad(60f) * MathUtils.Sin((float)Math.PI * MathUtils.Sigmoid(_kickPhase, 5f));
                num = MathUtils.Lerp(num, x, _kickFactor);
            }

            var num7 = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
            _legAngle1 += num7 * (num - _legAngle1);
            _legAngle2 += num7 * (num2 - _legAngle2);
            _headAngleY += num7 * (num3 - _headAngleY);
            var vector2 = ComponentCreature.ComponentLocomotion.LookAngles;
            vector2.Y += _headAngleY;
            if (_feedFactor > 0f)
            {
                var y = 0f - MathUtils.DegToRad(35f +
                                                55f * SimplexNoise.OctavedNoise((float)subsystemTime.GameTime, 3f, 2,
                                                    2f, 0.75f));
                vector2 = Vector2.Lerp(v2: new Vector2(0f, y), v1: vector2, f: _feedFactor);
            }

            vector2.X = MathUtils.Clamp(vector2.X, 0f - MathUtils.DegToRad(90f), MathUtils.DegToRad(90f));
            vector2.Y = MathUtils.Clamp(vector2.Y, 0f - MathUtils.DegToRad(90f), MathUtils.DegToRad(50f));
            var vector3 = Vector2.Zero;
            if (_neckBone != null)
            {
                vector3 = 0.4f * vector2;
                vector2 = 0.6f * vector2;
            }

            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position.X, position.Y + Bob, position.Z));
            SetBoneTransform(_headBone.Index,
                Matrix.CreateRotationX(vector2.Y) * Matrix.CreateRotationZ(0f - vector2.X));
            if (_neckBone != null)
            {
                SetBoneTransform(_neckBone.Index,
                    Matrix.CreateRotationX(vector3.Y) * Matrix.CreateRotationZ(0f - vector3.X));
            }

            SetBoneTransform(_leg1Bone.Index, Matrix.CreateRotationX(_legAngle1));
            SetBoneTransform(_leg2Bone.Index, Matrix.CreateRotationX(_legAngle2));
        }
        else
        {
            var num8 = 1f - DeathPhase;
            float num9 = Vector3.Dot(componentFrame.Matrix.Right, DeathCauseOffset) > 0f ? 1 : -1;
            var num10 = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                        ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateTranslation(-0.5f * num10 * DeathPhase * Vector3.UnitY) *
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI / 2f * DeathPhase * num9) *
                Matrix.CreateTranslation(0.2f * num10 * DeathPhase * Vector3.UnitY) *
                Matrix.CreateTranslation(position));
            SetBoneTransform(_headBone.Index, Matrix.Identity);
            if (_neckBone != null)
            {
                SetBoneTransform(_neckBone.Index, Matrix.Identity);
            }

            SetBoneTransform(_leg1Bone.Index, Matrix.CreateRotationX(_legAngle1 * num8));
            SetBoneTransform(_leg2Bone.Index, Matrix.CreateRotationX(_legAngle2 * num8));
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _walkLegsAngle = valuesDictionary.GetValue<float>("WalkLegsAngle");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyBone = Model.FindBone("Body")!;
        _neckBone = Model.FindBone("Neck", false);
        _headBone = Model.FindBone("Head")!;
        _leg1Bone = Model.FindBone("Leg1")!;
        _leg2Bone = Model.FindBone("Leg2")!;
    }
}
