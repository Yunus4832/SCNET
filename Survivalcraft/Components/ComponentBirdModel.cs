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
        var num = Vector3.Dot(
            ComponentCreature.ComponentBody.Velocity,
            ComponentCreature.ComponentBody.Matrix.Forward
        );
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
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) *
                Matrix.CreateTranslation(ComponentCreature.ComponentBody.Position + new Vector3(0f, Bob, 0f))
            );
            SetNodeTransform(_neckNode.Index, Matrix.CreateFromYawPitchRoll(yaw2, num4, 0f));
            SetNodeTransform(
                _headNode.Index,
                Matrix.CreateFromYawPitchRoll(
                    yaw,
                    num5 + MathUtils.Clamp(vector.Y, -(float)Math.PI / 4f, (float)Math.PI / 4f),
                    vector.Z
                )
            );
            if (_hasWings)
            {
                SetNodeTransform(_wing1Node!.Index, Matrix.CreateRotationY(num));
                SetNodeTransform(_wing2Node!.Index, Matrix.CreateRotationY(0f - num));
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(num2));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(num3));
        }
        else
        {
            var num7 = 1f - DeathPhase;
            var num8 = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                       ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            var position = ComponentCreature.ComponentBody.Position +
                           0.5f *
                           num8 *
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
                SetNodeTransform(_wing1Node!.Index, Matrix.CreateRotationY(num * num7));
                SetNodeTransform(_wing2Node!.Index, Matrix.CreateRotationY((0f - num) * num7));
            }

            SetNodeTransform(_leg1Node.Index, Matrix.CreateRotationX(num2 * num7));
            SetNodeTransform(_leg2Node.Index, Matrix.CreateRotationX(num3 * num7));
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
