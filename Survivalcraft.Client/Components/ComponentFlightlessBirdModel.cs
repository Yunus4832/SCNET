using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFlightlessBirdModel : ComponentCreatureModel
{
    private ModelNode _bodyNode = null!;

    private float _feedFactor;

    private float _footstepsPhase;

    private float _headAngleY;

    private ModelNode _headNode = null!;

    private float _kickFactor;

    private ModelNode _leg1Node = null!;

    private ModelNode _leg2Node = null!;

    private float _legAngle1;

    private float _legAngle2;

    private ModelNode? _neckNode;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private float _walkLegsAngle;

    public override void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var footstepsPhase = _footstepsPhase;
        var walkSpeed = ComponentCreature.ComponentLocomotion.SlipSpeed ??
                        Vector3.Dot(
                            ComponentCreature.ComponentBody.Velocity,
                            ComponentCreature.ComponentBody.Matrix.Forward
                        );
        if (MathUtils.Abs(walkSpeed) > 0.2f)
        {
            MovementAnimationPhase += walkSpeed * dt * _walkAnimationSpeed;
            _footstepsPhase += 1.25f * _walkAnimationSpeed * walkSpeed * dt;
        }
        else
        {
            MovementAnimationPhase = 0f;
            _footstepsPhase = 0f;
        }

        var targetBob = (0f - _walkBobHeight) *
                        MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        var bobBlend = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += bobBlend * (targetBob - Bob);
        var footstepBoundary = MathUtils.Floor(_footstepsPhase);
        if (WorldRuntimeContext.For(Project).HasPresentation &&
            _footstepsPhase > footstepBoundary &&
            footstepsPhase <= footstepBoundary)
        {
            ComponentCreature.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayFootstepSound(1f);
        }

        _feedFactor =
            creaturePose.LastFeedOrder
                ? MathUtils.Min(_feedFactor + 2f * dt, 1f)
                : MathUtils.Max(_feedFactor - 2f * dt, 0f);
        _kickFactor = creaturePose.LastAttackOrder
            ? MathUtils.Min(_kickFactor + 6f * dt, 1f)
            : MathUtils.Max(_kickFactor - 6f * dt, 0f);

        base.Update(dt);
    }

    public override void Animate()
    {
        var position = ComponentCreature.ComponentBody.Position;
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var firstLegPitch = 0f;
            var secondLegPitch = 0f;
            var headBobPitch = 0f;
            if (MovementAnimationPhase != 0f &&
                (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
                 ComponentCreature.ComponentBody.ImmersionFactor > 0f))
            {
                var strideAmplitude =
                    Vector3.Dot(
                        ComponentCreature.ComponentBody.Velocity,
                        ComponentCreature.ComponentBody.Matrix.Forward
                    ) >
                    0.75f * ComponentCreature.ComponentLocomotion.WalkSpeed
                        ? 1.5f * _walkLegsAngle
                        : _walkLegsAngle;
                var firstLegWave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0f));
                var secondLegWave = MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + 0.5f));
                firstLegPitch = strideAmplitude * firstLegWave + creaturePose.AttackPhase;
                secondLegPitch = strideAmplitude * secondLegWave;
                headBobPitch = MathUtils.DegToRad(5f) * MathUtils.Sin((float)Math.PI * 4f * MovementAnimationPhase);
            }

            if (_kickFactor != 0f)
            {
                var x = MathUtils.DegToRad(60f) *
                        MathUtils.Sin((float)Math.PI * MathUtils.Sigmoid(creaturePose.AttackPhase, 5f));
                firstLegPitch = MathUtils.Lerp(firstLegPitch, x, _kickFactor);
            }

            var poseBlend = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
            _legAngle1 += poseBlend * (firstLegPitch - _legAngle1);
            _legAngle2 += poseBlend * (secondLegPitch - _legAngle2);
            _headAngleY += poseBlend * (headBobPitch - _headAngleY);
            var headAngles = ComponentCreature.ComponentLocomotion.LookAngles;
            headAngles.Y += _headAngleY;
            if (_feedFactor > 0f)
            {
                var y = 0f -
                        MathUtils.DegToRad(
                            35f +
                            55f *
                            SimplexNoise.OctavedNoise(
                                (float)subsystemTime.GameTime,
                                3f,
                                2,
                                2f,
                                0.75f
                            )
                        );
                headAngles = Vector2.Lerp(v2: new Vector2(0f, y), v1: headAngles, f: _feedFactor);
            }

            headAngles.X = MathUtils.Clamp(headAngles.X, 0f - MathUtils.DegToRad(90f), MathUtils.DegToRad(90f));
            headAngles.Y = MathUtils.Clamp(headAngles.Y, 0f - MathUtils.DegToRad(90f), MathUtils.DegToRad(50f));
            var neckAngles = Vector2.Zero;
            if (_neckNode != null)
            {
                neckAngles = 0.4f * headAngles;
                headAngles = 0.6f * headAngles;
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
        }
        else
        {
            var uprightBlend = 1f - DeathPhase;
            float fallSide = Vector3.Dot(componentFrame.Matrix.Right, DeathCauseOffset) > 0f ? 1 : -1;
            var bodyHeight = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                             ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateTranslation(-0.5f * bodyHeight * DeathPhase * Vector3.UnitY) *
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI / 2f * DeathPhase * fallSide) *
                Matrix.CreateTranslation(0.2f * bodyHeight * DeathPhase * Vector3.UnitY) *
                Matrix.CreateTranslation(position)
            );
            SetNodeTransform(_headNode.Index, Matrix.Identity);
            if (_neckNode != null)
            {
                SetNodeTransform(_neckNode.Index, Matrix.Identity);
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(_legAngle1 * uprightBlend));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(_legAngle2 * uprightBlend));
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        base.Load(valuesDictionary, idToEntityMap);
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _walkLegsAngle = valuesDictionary.GetValue<float>("WalkLegsAngle");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyNode = Model.FindNode("Body")!;
        _neckNode = Model.FindNode("Neck", false);
        _headNode = Model.FindNode("Head")!;
        _leg1Node = Model.FindNode("Leg1")!;
        _leg2Node = Model.FindNode("Leg2")!;
    }
}
