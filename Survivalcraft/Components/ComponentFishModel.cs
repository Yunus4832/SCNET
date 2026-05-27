using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFishModel : ComponentCreatureModel
{
    public float? BendOrder;

    public float DigInOrder;

    private float _bitingPhase;

    private ModelBone _bodyBone = null!;

    private float _digInDepth;

    private float _digInTailPhase;

    private bool _hasVerticalTail;

    private ModelBone? _jawBone;

    private float _swimAnimationSpeed;

    private ModelBone _tail1Bone = null!;

    private ModelBone _tail2Bone = null!;

    private Vector2 _tailTurn;

    private float _tailWagPhase;

    public override void Update(float dt)
    {
        if (ComponentCreature.ComponentLocomotion.LastSwimOrder.HasValue &&
            ComponentCreature.ComponentLocomotion.LastSwimOrder.Value != Vector3.Zero)
        {
            var num = ComponentCreature.ComponentLocomotion.LastSwimOrder.Value.LengthSquared() > 0.99f ? 1.75f : 1f;
            MovementAnimationPhase =
                MathUtils.Remainder(MovementAnimationPhase + _swimAnimationSpeed * num * dt, 1000f);
        }
        else
        {
            MovementAnimationPhase =
                MathUtils.Remainder(MovementAnimationPhase + 0.15f * _swimAnimationSpeed * dt, 1000f);
        }

        if (BendOrder.HasValue)
        {
            if (_hasVerticalTail)
            {
                _tailTurn.X = 0f;
                _tailTurn.Y = BendOrder.Value;
            }
            else
            {
                _tailTurn.X = BendOrder.Value;
                _tailTurn.Y = 0f;
            }
        }
        else
        {
            _tailTurn.X += MathUtils.Saturate(2f * ComponentCreature.ComponentLocomotion.TurnSpeed * dt) *
                           (0f - ComponentCreature.ComponentLocomotion.LastTurnOrder.X - _tailTurn.X);
        }

        if (DigInOrder > _digInDepth)
        {
            var num2 = (DigInOrder - _digInDepth) * MathUtils.Min(1.5f * dt, 1f);
            _digInDepth += num2;
            _digInTailPhase += 20f * num2;
        }
        else if (DigInOrder < _digInDepth)
        {
            _digInDepth += (DigInOrder - _digInDepth) * MathUtils.Min(5f * dt, 1f);
        }

        var num3 = 0.33f * ComponentCreature.ComponentLocomotion.TurnSpeed;
        var num4 = 1f * ComponentCreature.ComponentLocomotion.TurnSpeed;
        IsAttackHitMoment = false;
        if (AttackOrder || FeedOrder)
        {
            if (AttackOrder)
            {
                _tailWagPhase = MathUtils.Remainder(_tailWagPhase + num3 * dt, 1f);
            }

            var bitingPhase = _bitingPhase;
            _bitingPhase = MathUtils.Remainder(_bitingPhase + num4 * dt, 1f);
            if (AttackOrder && bitingPhase < 0.5f && _bitingPhase >= 0.5f)
            {
                IsAttackHitMoment = true;
            }
        }
        else
        {
            if (_tailWagPhase != 0f)
            {
                _tailWagPhase = MathUtils.Remainder(MathUtils.Min(_tailWagPhase + num3 * dt, 1f), 1f);
            }

            if (_bitingPhase != 0f)
            {
                _bitingPhase = MathUtils.Remainder(MathUtils.Min(_bitingPhase + num4 * dt, 1f), 1f);
            }
        }

        LastAttackOrder = AttackOrder;
        LastFeedOrder = FeedOrder;
        AttackOrder = false;
        FeedOrder = false;
        BendOrder = null;
        DigInOrder = 0f;
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

        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var num = _digInTailPhase + _tailWagPhase;
            float num2;
            float num3;
            float num4;
            float num5;
            if (_hasVerticalTail)
            {
                num2 = MathUtils.DegToRad(25f) *
                       MathUtils.Clamp(0.5f * MathUtils.Sin((float)Math.PI * 2f * num) - _tailTurn.X, -1f, 1f);
                num3 = MathUtils.DegToRad(30f) *
                       MathUtils.Clamp(
                           0.5f * MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(num - 0.25f, 0f))) - _tailTurn.X,
                           -1f, 1f);
                num4 = MathUtils.DegToRad(25f) *
                       MathUtils.Clamp(
                           0.5f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase) - _tailTurn.Y, -1f, 1f);
                num5 = MathUtils.DegToRad(30f) *
                       MathUtils.Clamp(
                           0.5f * MathUtils.Sin((float)Math.PI * 2f *
                                                MathUtils.Max(MovementAnimationPhase - 0.25f, 0f)) - _tailTurn.Y, -1f,
                           1f);
            }
            else
            {
                num2 = MathUtils.DegToRad(25f) *
                       MathUtils.Clamp(
                           0.5f * MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + num)) - _tailTurn.X,
                           -1f, 1f);
                num3 = MathUtils.DegToRad(30f) *
                       MathUtils.Clamp(
                           0.5f * MathUtils.Sin(2f *
                                                ((float)Math.PI * MathUtils.Max(MovementAnimationPhase + num - 0.25f,
                                                    0f))) - _tailTurn.X, -1f, 1f);
                num4 = MathUtils.DegToRad(25f) * MathUtils.Clamp(0f - _tailTurn.Y, -1f, 1f);
                num5 = MathUtils.DegToRad(30f) * MathUtils.Clamp(0f - _tailTurn.Y, -1f, 1f);
            }

            var radians = 0f;
            if (_bitingPhase > 0f)
            {
                radians = (0f - MathUtils.DegToRad(30f)) * MathUtils.Sin((float)Math.PI * _bitingPhase);
            }

            var value = Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) *
                        Matrix.CreateTranslation(ComponentCreature.ComponentBody.Position +
                                                 new Vector3(0f, 0f - _digInDepth, 0f));
            SetBoneTransform(_bodyBone.Index, value);
            var identity = Matrix.Identity;
            if (num2 != 0f)
            {
                identity *= Matrix.CreateRotationZ(num2);
            }

            if (num4 != 0f)
            {
                identity *= Matrix.CreateRotationX(num4);
            }

            var identity2 = Matrix.Identity;
            if (num3 != 0f)
            {
                identity2 *= Matrix.CreateRotationZ(num3);
            }

            if (num5 != 0f)
            {
                identity2 *= Matrix.CreateRotationX(num5);
            }

            SetBoneTransform(_tail1Bone.Index, identity);
            SetBoneTransform(_tail2Bone.Index, identity2);
            if (_jawBone != null)
            {
                SetBoneTransform(_jawBone.Index, Matrix.CreateRotationX(radians));
            }
        }
        else
        {
            var num6 = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                       ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            var position = ComponentCreature.ComponentBody.Position + 1f * num6 * DeathPhase * Vector3.UnitY;
            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI * DeathPhase) *
                Matrix.CreateTranslation(position));
            SetBoneTransform(_tail1Bone.Index, Matrix.Identity);
            SetBoneTransform(_tail2Bone.Index, Matrix.Identity);
            if (_jawBone != null)
            {
                SetBoneTransform(_jawBone.Index, Matrix.Identity);
            }
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _hasVerticalTail = valuesDictionary.GetValue<bool>("HasVerticalTail");
        _swimAnimationSpeed = valuesDictionary.GetValue<float>("SwimAnimationSpeed");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyBone = Model.FindBone("Body")!;
        _tail1Bone = Model.FindBone("Tail1")!;
        _tail2Bone = Model.FindBone("Tail2")!;
        _jawBone = Model.FindBone("Jaw", false);
    }

    public override Vector3 CalculateEyePosition()
    {
        var matrix = ComponentCreature.ComponentBody.Matrix;
        return ComponentCreature.ComponentBody.Position +
               matrix.Up * 1f * ComponentCreature.ComponentBody.BoxSize.Y +
               matrix.Forward * 0.45f * ComponentCreature.ComponentBody.BoxSize.Z;
    }
}
