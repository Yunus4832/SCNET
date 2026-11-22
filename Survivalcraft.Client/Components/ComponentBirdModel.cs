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

    private ModelNode _bodyNode = null!;

    private ModelNode _headNode = null!;

    private ModelNode _leg1Node = null!;

    private ModelNode _leg2Node = null!;

    private ModelNode _neckNode = null!;

    private ModelNode? _wing1Node;

    private ModelNode? _wing2Node;

    public float FlyPhase { get; set; }

    public override void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var forwardSpeed = Vector3.Dot(
            ComponentCreature.ComponentBody.Velocity,
            ComponentCreature.ComponentBody.Matrix.Forward
        );
        if (MathUtils.Abs(forwardSpeed) > 0.1f)
        {
            MovementAnimationPhase += forwardSpeed * dt * _walkAnimationSpeed;
        }
        else
        {
            var previousStride = MathUtils.Floor(MovementAnimationPhase);
            if (MovementAnimationPhase.UncloseTo(previousStride))
            {
                MovementAnimationPhase = MovementAnimationPhase - previousStride > 0.5f
                    ? MathUtils.Min(MovementAnimationPhase + 2f * dt, previousStride + 1f)
                    : MathUtils.Max(MovementAnimationPhase - 2f * dt, previousStride);
            }
        }

        var targetBob = (0f - _walkBobHeight) *
                        MathUtils.Sqr(MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase));
        var bobBlend = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += bobBlend * (targetBob - Bob);
        if (_hasWings)
        {
            if (ComponentCreature.ComponentLocomotion.LastFlyOrder.HasValue)
            {
                var flapSpeedFactor = ComponentCreature.ComponentLocomotion.LastFlyOrder.Value.LengthSquared() > 0.99f
                    ? 1.5f
                    : 1f;
                FlyPhase = MathUtils.Remainder(FlyPhase + _flyAnimationSpeed * flapSpeedFactor * dt, 1f);
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

        if (creaturePose.LastFeedOrder)
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

        base.Update(dt);
    }

    public override void Animate()
    {
        var wingAngle = 0f;
        if (_hasWings)
        {
            wingAngle += 1.2f * MathUtils.Sin((float)Math.PI * 2f * (FlyPhase + 0.75f));
            if (ComponentCreature.ComponentBody.StandingOnValue.HasValue)
            {
                wingAngle += 0.3f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            }
        }

        float firstLegPitch;
        float secondLegPitch;
        if (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
            ComponentCreature.ComponentBody.ImmersionFactor > 0f ||
            ComponentCreature.ComponentLocomotion.FlySpeed == 0f)
        {
            firstLegPitch = 0.6f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            secondLegPitch = 0f - firstLegPitch;
        }
        else
        {
            firstLegPitch = secondLegPitch = 0f - MathUtils.DegToRad(60f);
        }

        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var yaw = ComponentCreature.ComponentLocomotion.LookAngles.X / 2f;
            var yaw2 = ComponentCreature.ComponentLocomotion.LookAngles.X / 2f;
            var neckPitch = 0f;
            var headPitch = 0f;
            if (ComponentCreature.ComponentBody.StandingOnValue.HasValue ||
                ComponentCreature.ComponentBody.ImmersionFactor > 0f)
            {
                neckPitch = 0.5f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase / 2f);
                headPitch = 0f - neckPitch;
            }

            var peckWave = MathUtils.Cos((float)Math.PI * 2f * _peckPhase);
            neckPitch -= 1.25f * (1f - (peckWave >= 0f ? peckWave : -0.5f * peckWave));
            neckPitch += ComponentCreature.ComponentLocomotion.LookAngles.Y;
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) *
                Matrix.CreateTranslation(ComponentCreature.ComponentBody.Position + new Vector3(0f, Bob, 0f))
            );
            SetNodeTransform(_neckNode.Index, Matrix.CreateFromYawPitchRoll(yaw2, neckPitch, 0f));
            SetNodeTransform(
                _headNode.Index,
                Matrix.CreateFromYawPitchRoll(
                    yaw,
                    headPitch + MathUtils.Clamp(vector.Y, -(float)Math.PI / 4f, (float)Math.PI / 4f),
                    vector.Z
                )
            );
            if (_hasWings)
            {
                SetNodeTransform(_wing1Node!.Index, Matrix.CreateRotationY(wingAngle));
                SetNodeTransform(_wing2Node!.Index, Matrix.CreateRotationY(0f - wingAngle));
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(firstLegPitch));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(secondLegPitch));
        }
        else
        {
            var uprightBlend = 1f - DeathPhase;
            var bodyHeight = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                             ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            var position = ComponentCreature.ComponentBody.Position +
                           0.5f *
                           bodyHeight *
                           Vector3.Normalize(ComponentCreature.ComponentBody.Matrix.Forward * new Vector3(1f, 0f, 1f));
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, (float)Math.PI / 2f * DeathPhase, 0f) *
                Matrix.CreateTranslation(position)
            );
            SetNodeTransform(_neckNode.Index, Matrix.Identity);
            SetNodeTransform(_headNode.Index, Matrix.Identity);
            if (_hasWings)
            {
                SetNodeTransform(_wing1Node!.Index, Matrix.CreateRotationY(wingAngle * uprightBlend));
                SetNodeTransform(_wing2Node!.Index, Matrix.CreateRotationY((0f - wingAngle) * uprightBlend));
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(firstLegPitch * uprightBlend));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(secondLegPitch * uprightBlend));
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        base.Load(valuesDictionary, idToEntityMap);
        _flyAnimationSpeed = valuesDictionary.GetValue<float>("FlyAnimationSpeed");
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _peckAnimationSpeed = valuesDictionary.GetValue<float>("PeckAnimationSpeed");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyNode = Model.FindNode("Body")!;
        _neckNode = Model.FindNode("Neck")!;
        _headNode = Model.FindNode("Head")!;
        _leg1Node = Model.FindNode("Leg1")!;
        _leg2Node = Model.FindNode("Leg2")!;
        _wing1Node = Model.FindNode("Wing1", false);
        _wing2Node = Model.FindNode("Wing2", false);
        _hasWings = _wing1Node != null && _wing2Node != null;
    }
}
