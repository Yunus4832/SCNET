using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFourLeggedModel : ComponentCreatureModel
{
    public enum Gait
    {
        Walk = 0,

        Trot = 1,

        Canter = 2
    }

    public float LastMove;

    private ModelNode _bodyNode = null!;

    private float _buttFactor;

    private bool _canCanter;

    private float _canterLegsAngleFactor;

    private bool _canTrot;

    private float _feedFactor;

    private float _footstepsPhase;

    private Gait _gait;

    private float _headAngleY;

    private ModelNode _headNode = null!;

    private ModelNode _leg1Node = null!;

    private ModelNode _leg2Node = null!;

    private ModelNode _leg3Node = null!;

    private ModelNode _leg4Node = null!;

    private float _legAngle1;

    private float _legAngle2;

    private float _legAngle3;

    private float _legAngle4;

    private bool _moveLegWhenFeeding;

    private ModelNode? _neckNode;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private bool _useCanterSound;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private float _walkFrontLegsAngle;

    private float _walkHindLegsAngle;

    public override void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var footstepsPhase = _footstepsPhase;
        var walkSpeed = ComponentCreature.ComponentLocomotion.SlipSpeed ??
                        Vector3.Dot(
                            ComponentCreature.ComponentBody.Velocity,
                            ComponentCreature.ComponentBody.Matrix.Forward
                        );
        if (_canCanter && walkSpeed > 0.7f * ComponentCreature.ComponentLocomotion.WalkSpeed)
        {
            _gait = Gait.Canter;
            MovementAnimationPhase += walkSpeed * dt * 0.7f * _walkAnimationSpeed;
            _footstepsPhase += 0.7f * _walkAnimationSpeed * walkSpeed * dt;
        }
        else if (_canTrot && walkSpeed > 0.5f * ComponentCreature.ComponentLocomotion.WalkSpeed)
        {
            _gait = Gait.Trot;
            MovementAnimationPhase += walkSpeed * dt * _walkAnimationSpeed;
            _footstepsPhase += 1.25f * _walkAnimationSpeed * walkSpeed * dt;
        }
        else if (MathUtils.Abs(walkSpeed) > 0.2f)
        {
            _gait = Gait.Walk;
            MovementAnimationPhase += walkSpeed * dt * _walkAnimationSpeed;
            _footstepsPhase += 1.25f * _walkAnimationSpeed * walkSpeed * dt;
        }
        else
        {
            _gait = Gait.Walk;
            MovementAnimationPhase = 0f;
            _footstepsPhase = 0f;
        }

        var targetBob = 0f;
        if (_gait == Gait.Canter)
        {
            targetBob = (0f - _walkBobHeight) * 1.5f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
        }
        else if (_gait == Gait.Trot)
        {
            targetBob = _walkBobHeight *
                        1.5f *
                        MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        }
        else if (_gait == Gait.Walk)
        {
            targetBob = (0f - _walkBobHeight) *
                        MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        }

        var bobBlend = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += bobBlend * (targetBob - Bob);
        if (WorldRuntimeContext.For(Project).HasPresentation)
        {
            if (_gait == Gait.Canter && _useCanterSound)
            {
                var canterStepBoundary = MathUtils.Floor(_footstepsPhase);
                if (_footstepsPhase > canterStepBoundary && footstepsPhase <= canterStepBoundary)
                {
                    var footstepSoundMaterialName =
                        _subsystemSoundMaterials.GetFootstepSoundMaterialName(ComponentCreature);
                    if (!string.IsNullOrEmpty(footstepSoundMaterialName) && footstepSoundMaterialName != "Water")
                    {
                        _subsystemAudio.PlayRandomSound(
                            "Audio/Footsteps/CanterDirt",
                            0.75f,
                            random.Float(-0.25f, 0f),
                            ComponentCreature.ComponentBody.Position,
                            3f,
                            true
                        );
                    }
                }
            }
            else
            {
                var stepBoundary = MathUtils.Floor(_footstepsPhase);
                if (_footstepsPhase > stepBoundary && footstepsPhase <= stepBoundary)
                {
                    ComponentCreature.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!
                        .PlayFootstepSound(1f);
                }
            }
        }

        _feedFactor =
            creaturePose.LastFeedOrder
                ? MathUtils.Min(_feedFactor + 2f * dt, 1f)
                : MathUtils.Max(_feedFactor - 2f * dt, 0f);
        _buttFactor = creaturePose.LastAttackOrder
            ? MathUtils.Min(_buttFactor + 4f * dt, 1f)
            : MathUtils.Max(_buttFactor - 4f * dt, 0f);

        base.Update(dt);
    }

    public override void Animate()
    {
        var position = ComponentCreature.ComponentBody.Position;
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var frontLeg1Pitch = 0f;
            var frontLeg2Pitch = 0f;
            var hindLeg1Pitch = 0f;
            var hindLeg2Pitch = 0f;
            var headBobPitch = 0f;
            if (MovementAnimationPhase != 0f &&
                (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
                 ComponentCreature.ComponentBody.ImmersionFactor > 0f))
            {
                if (_gait == Gait.Canter)
                {
                    var frontLeg1Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    var frontLeg2Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.25f));
                    var hindLeg1Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.15f));
                    var hindLeg2Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.4f));
                    frontLeg1Pitch = _walkFrontLegsAngle * _canterLegsAngleFactor * frontLeg1Wave;
                    frontLeg2Pitch = _walkFrontLegsAngle * _canterLegsAngleFactor * frontLeg2Wave;
                    hindLeg1Pitch = _walkHindLegsAngle * _canterLegsAngleFactor * hindLeg1Wave;
                    hindLeg2Pitch = _walkHindLegsAngle * _canterLegsAngleFactor * hindLeg2Wave;
                    headBobPitch = MathUtils.DegToRad(8f) * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
                }
                else if (_gait == Gait.Trot)
                {
                    var frontLeg1Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    var frontLeg2Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                    var hindLeg1Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                    var hindLeg2Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    frontLeg1Pitch = _walkFrontLegsAngle * frontLeg1Wave;
                    frontLeg2Pitch = _walkFrontLegsAngle * frontLeg2Wave;
                    hindLeg1Pitch = _walkHindLegsAngle * hindLeg1Wave;
                    hindLeg2Pitch = _walkHindLegsAngle * hindLeg2Wave;
                    headBobPitch = MathUtils.DegToRad(3f) * MathUtils.Sin((float)Math.PI * 4f * MovementAnimationPhase);
                }
                else
                {
                    var frontLeg1Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                    var frontLeg2Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                    var hindLeg1Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.25f));
                    var hindLeg2Wave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.75f));
                    frontLeg1Pitch = _walkFrontLegsAngle * frontLeg1Wave;
                    frontLeg2Pitch = _walkFrontLegsAngle * frontLeg2Wave;
                    hindLeg1Pitch = _walkHindLegsAngle * hindLeg1Wave;
                    hindLeg2Pitch = _walkHindLegsAngle * hindLeg2Wave;
                    headBobPitch = MathUtils.DegToRad(3f) * MathUtils.Sin((float)Math.PI * 4f * MovementAnimationPhase);
                }
            }

            var poseBlend = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
            _legAngle1 += poseBlend * (frontLeg1Pitch - _legAngle1);
            _legAngle2 += poseBlend * (frontLeg2Pitch - _legAngle2);
            _legAngle3 += poseBlend * (hindLeg1Pitch - _legAngle3);
            _legAngle4 += poseBlend * (hindLeg2Pitch - _legAngle4);
            _headAngleY += poseBlend * (headBobPitch - _headAngleY);
            var headAngles = ComponentCreature.ComponentLocomotion.LookAngles;
            headAngles.Y += _headAngleY;
            headAngles.X = MathUtils.Clamp(headAngles.X, 0f - MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
            headAngles.Y = MathUtils.Clamp(headAngles.Y, 0f - MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));
            var neckAngles = Vector2.Zero;
            if (_neckNode != null)
            {
                neckAngles = 0.6f * headAngles;
                headAngles = 0.4f * headAngles;
            }

            if (_feedFactor > 0f)
            {
                var y = 0f -
                        MathUtils.DegToRad(
                            25f +
                            45f *
                            SimplexNoise.OctavedNoise(
                                (float)subsystemTime.GameTime,
                                3f,
                                2,
                                2f,
                                0.75f
                            )
                        );
                headAngles = Vector2.Lerp(v2: new Vector2(0f, y), v1: headAngles, f: _feedFactor);
                if (_moveLegWhenFeeding)
                {
                    var x = MathUtils.DegToRad(20f) +
                            MathUtils.PowSign(
                                SimplexNoise.OctavedNoise((float)subsystemTime.GameTime, 1f, 1, 1f, 1f) - 0.5f,
                                0.33f
                            ) /
                            0.5f *
                            MathUtils.DegToRad(25f) *
                            (float)MathUtils.Sin(17.0 * subsystemTime.GameTime);
                    _ = MathUtils.Lerp(frontLeg2Pitch, x, _feedFactor);
                }
            }

            if (_buttFactor != 0f)
            {
                var y2 = (0f - MathUtils.DegToRad(40f)) *
                         MathUtils.Sin((float)Math.PI * 2f * MathUtils.Sigmoid(creaturePose.AttackPhase, 4f));
                headAngles = Vector2.Lerp(v2: new Vector2(0f, y2), v1: headAngles, f: _buttFactor);
            }

            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position.X, position.Y + Bob, position.Z)
            );
            SetNodeTransform(
                _headNode.Index,
                Matrix.CreateRotationX(headAngles.Y) * Matrix.CreateRotationZ(0f - headAngles.X)
            );
            if (_neckNode != null)
            {
                SetNodeTransform(
                    _neckNode.Index,
                    Matrix.CreateRotationX(neckAngles.Y) * Matrix.CreateRotationZ(0f - neckAngles.X)
                );
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(_legAngle1));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(_legAngle2));
            SetNodeTransform(_leg3Node.Index, Matrix.CreateRotationX(_legAngle3));
            SetNodeTransform(_leg4Node.Index, Matrix.CreateRotationX(_legAngle4));
        }
        else
        {
            var uprightBlend = 1f - DeathPhase;
            float fallSide = Vector3.Dot(componentFrame.Matrix.Right, DeathCauseOffset) > 0f ? 1 : -1;
            var bodyHeight = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                             ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateTranslation(-0.5f * bodyHeight * Vector3.UnitY * DeathPhase) *
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI / 2f * DeathPhase * fallSide) *
                Matrix.CreateTranslation(0.2f * bodyHeight * Vector3.UnitY * DeathPhase) *
                Matrix.CreateTranslation(position)
            );
            SetNodeTransform(_headNode.Index, Matrix.CreateRotationX(MathUtils.DegToRad(50f) * DeathPhase));
            if (_neckNode != null)
            {
                SetNodeTransform(_neckNode.Index, Matrix.Identity);
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(_legAngle1 * uprightBlend));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(_legAngle2 * uprightBlend));
            SetNodeTransform(_leg3Node.Index, Matrix.CreateRotationX(_legAngle3 * uprightBlend));
            SetNodeTransform(_leg4Node.Index, Matrix.CreateRotationX(_legAngle4 * uprightBlend));
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

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
        _bodyNode = Model.FindNode("Body")!;
        _neckNode = Model.FindNode("Neck", false);
        _headNode = Model.FindNode("Head")!;
        _leg1Node = Model.FindNode("Leg1")!;
        _leg2Node = Model.FindNode("Leg2")!;
        _leg3Node = Model.FindNode("Leg3")!;
        _leg4Node = Model.FindNode("Leg4")!;
    }
}
