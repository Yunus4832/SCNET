using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFourLeggedModel : ComponentCreatureModel
{
    public enum Gait
    {
        Walk,
        Trot,
        Canter
    }

    public float LastMove;

    private ModelBone _bodyBone = null!;

    private float _buttFactor;

    private float _buttPhase;

    private bool _canCanter;

    private float _canterLegsAngleFactor;

    private bool _canTrot;

    private float _feedFactor;

    private float _footstepsPhase;

    private Gait _gait;

    private float _headAngleY;

    private ModelBone _headBone = null!;

    private ModelBone _leg1Bone = null!;

    private ModelBone _leg2Bone = null!;

    private ModelBone _leg3Bone = null!;

    private ModelBone _leg4Bone = null!;

    private float _legAngle1;

    private float _legAngle2;

    private float _legAngle3;

    private float _legAngle4;

    private bool _moveLegWhenFeeding;

    private ModelBone? _neckBone;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private bool _useCanterSound;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private float _walkFrontLegsAngle;

    private float _walkHindLegsAngle;

    public override void Update(float dt)
    {
        var footstepsPhase = _footstepsPhase;
        var num = ComponentCreature.ComponentLocomotion.SlipSpeed ?? Vector3.Dot(
            ComponentCreature.ComponentBody.Velocity, ComponentCreature.ComponentBody.Matrix.Forward);
        if (_canCanter && num > 0.7f * ComponentCreature.ComponentLocomotion.WalkSpeed)
        {
            _gait = Gait.Canter;
            MovementAnimationPhase += num * dt * 0.7f * _walkAnimationSpeed;
            _footstepsPhase += 0.7f * _walkAnimationSpeed * num * dt;
        }
        else if (_canTrot && num > 0.5f * ComponentCreature.ComponentLocomotion.WalkSpeed)
        {
            _gait = Gait.Trot;
            MovementAnimationPhase += num * dt * _walkAnimationSpeed;
            _footstepsPhase += 1.25f * _walkAnimationSpeed * num * dt;
        }
        else if (MathUtils.Abs(num) > 0.2f)
        {
            _gait = Gait.Walk;
            MovementAnimationPhase += num * dt * _walkAnimationSpeed;
            _footstepsPhase += 1.25f * _walkAnimationSpeed * num * dt;
        }
        else
        {
            _gait = Gait.Walk;
            MovementAnimationPhase = 0f;
            _footstepsPhase = 0f;
        }

        var num2 = 0f;
        if (_gait == Gait.Canter)
        {
            num2 = (0f - _walkBobHeight) * 1.5f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
        }
        else if (_gait == Gait.Trot)
        {
            num2 = _walkBobHeight * 1.5f * MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        }
        else if (_gait == Gait.Walk)
        {
            num2 = (0f - _walkBobHeight) * MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        }

        var num3 = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += num3 * (num2 - Bob);
        if (_gait == Gait.Canter && _useCanterSound)
        {
            var num4 = MathUtils.Floor(_footstepsPhase);
            if (_footstepsPhase > num4 && footstepsPhase <= num4)
            {
                var footstepSoundMaterialName =
                    _subsystemSoundMaterials.GetFootstepSoundMaterialName(ComponentCreature);
                if (!string.IsNullOrEmpty(footstepSoundMaterialName) && footstepSoundMaterialName != "Water")
                {
                    _subsystemAudio.PlayRandomSound("Audio/Footsteps/CanterDirt", 0.75f, random.Float(-0.25f, 0f),
                        ComponentCreature.ComponentBody.Position, 3f, true);
                }
            }
        }
        else
        {
            var num5 = MathUtils.Floor(_footstepsPhase);
            if (_footstepsPhase > num5 && footstepsPhase <= num5)
            {
                ComponentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
            }
        }

        _feedFactor =
            FeedOrder ? MathUtils.Min(_feedFactor + 2f * dt, 1f) : MathUtils.Max(_feedFactor - 2f * dt, 0f);
        IsAttackHitMoment = false;
        if (AttackOrder)
        {
            _buttFactor = MathUtils.Min(_buttFactor + 4f * dt, 1f);
            var buttPhase = _buttPhase;
            _buttPhase = MathUtils.Remainder(_buttPhase + dt * 2f, 1f);
            if (buttPhase < 0.5f && _buttPhase >= 0.5f)
            {
                IsAttackHitMoment = true;
            }
        }
        else
        {
            _buttFactor = MathUtils.Max(_buttFactor - 4f * dt, 0f);
            if (_buttPhase != 0f)
            {
                if (_buttPhase > 0.5f)
                {
                    _buttPhase = MathUtils.Remainder(MathUtils.Min(_buttPhase + dt * 2f, 1f), 1f);
                }
                else if (_buttPhase > 0f)
                {
                    _buttPhase = MathUtils.Max(_buttPhase - dt * 2f, 0f);
                }
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
        var position = ComponentCreature.ComponentBody.Position;
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var num = 0f;
            var num2 = 0f;
            var num3 = 0f;
            var num4 = 0f;
            var num5 = 0f;
            if (MovementAnimationPhase != 0f && (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
                                                 ComponentCreature.ComponentBody.ImmersionFactor > 0f))
            {
                if (_gait == Gait.Canter)
                {
                    var num6 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    var num7 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.25f));
                    var num8 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.15f));
                    var num9 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.4f));
                    num = _walkFrontLegsAngle * _canterLegsAngleFactor * num6;
                    num2 = _walkFrontLegsAngle * _canterLegsAngleFactor * num7;
                    num3 = _walkHindLegsAngle * _canterLegsAngleFactor * num8;
                    num4 = _walkHindLegsAngle * _canterLegsAngleFactor * num9;
                    num5 = MathUtils.DegToRad(8f) * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
                }
                else if (_gait == Gait.Trot)
                {
                    var num10 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    var num11 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                    var num12 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                    var num13 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    num = _walkFrontLegsAngle * num10;
                    num2 = _walkFrontLegsAngle * num11;
                    num3 = _walkHindLegsAngle * num12;
                    num4 = _walkHindLegsAngle * num13;
                    num5 = MathUtils.DegToRad(3f) * MathUtils.Sin((float)Math.PI * 4f * MovementAnimationPhase);
                }
                else
                {
                    var num14 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    var num15 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                    var num16 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.25f));
                    var num17 = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.75f));
                    num = _walkFrontLegsAngle * num14;
                    num2 = _walkFrontLegsAngle * num15;
                    num3 = _walkHindLegsAngle * num16;
                    num4 = _walkHindLegsAngle * num17;
                    num5 = MathUtils.DegToRad(3f) * MathUtils.Sin((float)Math.PI * 4f * MovementAnimationPhase);
                }
            }

            var num18 = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
            _legAngle1 += num18 * (num - _legAngle1);
            _legAngle2 += num18 * (num2 - _legAngle2);
            _legAngle3 += num18 * (num3 - _legAngle3);
            _legAngle4 += num18 * (num4 - _legAngle4);
            _headAngleY += num18 * (num5 - _headAngleY);
            var vector2 = ComponentCreature.ComponentLocomotion.LookAngles;
            vector2.Y += _headAngleY;
            vector2.X = MathUtils.Clamp(vector2.X, 0f - MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
            vector2.Y = MathUtils.Clamp(vector2.Y, 0f - MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));
            var vector3 = Vector2.Zero;
            if (_neckBone != null)
            {
                vector3 = 0.6f * vector2;
                vector2 = 0.4f * vector2;
            }

            if (_feedFactor > 0f)
            {
                var y = 0f - MathUtils.DegToRad(25f +
                                                45f * SimplexNoise.OctavedNoise((float)subsystemTime.GameTime, 3f, 2,
                                                    2f, 0.75f));
                vector2 = Vector2.Lerp(v2: new Vector2(0f, y), v1: vector2, f: _feedFactor);
                if (_moveLegWhenFeeding)
                {
                    var x = MathUtils.DegToRad(20f) +
                            MathUtils.PowSign(
                                SimplexNoise.OctavedNoise((float)subsystemTime.GameTime, 1f, 1, 1f, 1f) - 0.5f,
                                0.33f) / 0.5f * MathUtils.DegToRad(25f) *
                            (float)MathUtils.Sin(17.0 * subsystemTime.GameTime);
                    _ = MathUtils.Lerp(num2, x, _feedFactor);
                }
            }

            if (_buttFactor != 0f)
            {
                var y2 = (0f - MathUtils.DegToRad(40f)) *
                         MathUtils.Sin((float)Math.PI * 2f * MathUtils.Sigmoid(_buttPhase, 4f));
                vector2 = Vector2.Lerp(v2: new Vector2(0f, y2), v1: vector2, f: _buttFactor);
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
            SetBoneTransform(_leg3Bone.Index, Matrix.CreateRotationX(_legAngle3));
            SetBoneTransform(_leg4Bone.Index, Matrix.CreateRotationX(_legAngle4));
        }
        else
        {
            var num19 = 1f - DeathPhase;
            float num20 = Vector3.Dot(componentFrame.Matrix.Right, DeathCauseOffset) > 0f ? 1 : -1;
            var num21 = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                        ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateTranslation(-0.5f * num21 * Vector3.UnitY * DeathPhase) *
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI / 2f * DeathPhase * num20) *
                Matrix.CreateTranslation(0.2f * num21 * Vector3.UnitY * DeathPhase) *
                Matrix.CreateTranslation(position));
            SetBoneTransform(_headBone.Index, Matrix.CreateRotationX(MathUtils.DegToRad(50f) * DeathPhase));
            if (_neckBone != null)
            {
                SetBoneTransform(_neckBone.Index, Matrix.Identity);
            }

            SetBoneTransform(_leg1Bone.Index, Matrix.CreateRotationX(_legAngle1 * num19));
            SetBoneTransform(_leg2Bone.Index, Matrix.CreateRotationX(_legAngle2 * num19));
            SetBoneTransform(_leg3Bone.Index, Matrix.CreateRotationX(_legAngle3 * num19));
            SetBoneTransform(_leg4Bone.Index, Matrix.CreateRotationX(_legAngle4 * num19));
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _walkFrontLegsAngle = valuesDictionary.GetValue<float>("WalkFrontLegsAngle");
        _walkHindLegsAngle = valuesDictionary.GetValue<float>("WalkHindLegsAngle");
        _canterLegsAngleFactor = valuesDictionary.GetValue<float>("CanterLegsAngleFactor");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
        _moveLegWhenFeeding = valuesDictionary.GetValue<bool>("MoveLegWhenFeeding");
        _canCanter = valuesDictionary.GetValue<bool>("CanCanter");
        _canTrot = valuesDictionary.GetValue<bool>("CanTrot");
        _useCanterSound = valuesDictionary.GetValue<bool>("UseCanterSound");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyBone = Model.FindBone("Body")!;
        _neckBone = Model.FindBone("Neck", false);
        _headBone = Model.FindBone("Head")!;
        _leg1Bone = Model.FindBone("Leg1")!;
        _leg2Bone = Model.FindBone("Leg2")!;
        _leg3Bone = Model.FindBone("Leg3")!;
        _leg4Bone = Model.FindBone("Leg4")!;
    }
}
