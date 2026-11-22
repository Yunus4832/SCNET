using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFishModel : ComponentCreatureModel
{
    private ModelNode _bodyNode = null!;

    private float _digInDepth;

    private float _digInTailPhase;

    private bool _hasVerticalTail;

    private ModelNode? _jawNode;

    private float _swimAnimationSpeed;

    private ModelNode _tail1Node = null!;

    private ModelNode _tail2Node = null!;

    private Vector2 _tailTurn;

    private float _tailWagPhase;

    public override void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }


        if (ComponentCreature.ComponentLocomotion.LastSwimOrder.HasValue &&
            ComponentCreature.ComponentLocomotion.LastSwimOrder.Value != Vector3.Zero)
        {
            var swimSpeedFactor = ComponentCreature.ComponentLocomotion.LastSwimOrder.Value.LengthSquared() > 0.99f
                ? 1.75f
                : 1f;
            MovementAnimationPhase =
                MathUtils.Remainder(MovementAnimationPhase + _swimAnimationSpeed * swimSpeedFactor * dt, 1000f);
        }
        else
        {
            MovementAnimationPhase =
                MathUtils.Remainder(MovementAnimationPhase + 0.15f * _swimAnimationSpeed * dt, 1000f);
        }

        if (creaturePose.LastBendOrder.HasValue)
        {
            if (_hasVerticalTail)
            {
                _tailTurn.X = 0f;
                _tailTurn.Y = creaturePose.LastBendOrder.Value;
            }
            else
            {
                _tailTurn.X = creaturePose.LastBendOrder.Value;
                _tailTurn.Y = 0f;
            }
        }
        else
        {
            _tailTurn.X += MathUtils.Saturate(2f * ComponentCreature.ComponentLocomotion.TurnSpeed * dt) *
                           (0f - ComponentCreature.ComponentLocomotion.LastTurnOrder.X - _tailTurn.X);
        }

        if (creaturePose.LastDigInOrder > _digInDepth)
        {
            var depthChange = (creaturePose.LastDigInOrder - _digInDepth) * MathUtils.Min(1.5f * dt, 1f);
            _digInDepth += depthChange;
            _digInTailPhase += 20f * depthChange;
        }
        else if (creaturePose.LastDigInOrder < _digInDepth)
        {
            _digInDepth += (creaturePose.LastDigInOrder - _digInDepth) * MathUtils.Min(5f * dt, 1f);
        }

        var tailWagSpeed = 0.33f * ComponentCreature.ComponentLocomotion.TurnSpeed;
        var turnRate = 1f * ComponentCreature.ComponentLocomotion.TurnSpeed;
        if (creaturePose.LastAttackOrder) { _tailWagPhase = MathUtils.Remainder(_tailWagPhase + tailWagSpeed * dt, 1); }
        else { _tailWagPhase = MathUtils.Remainder(MathUtils.Min(_tailWagPhase + tailWagSpeed * dt, 1), 1); }

        base.Update(dt);
    }

    public override void Animate()
    {
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (ComponentCreature.ComponentHealth.Health > 0f)
        {
            var tailPhase = _digInTailPhase + _tailWagPhase;
            float firstTailRoll;
            float secondTailRoll;
            float firstTailPitch;
            float secondTailPitch;
            if (_hasVerticalTail)
            {
                firstTailRoll = MathUtils.DegToRad(25f) *
                                MathUtils.Clamp(
                                    0.5f * MathUtils.Sin((float)Math.PI * 2f * tailPhase) - _tailTurn.X,
                                    -1f,
                                    1f
                                );
                secondTailRoll = MathUtils.DegToRad(30f) *
                                 MathUtils.Clamp(
                                     0.5f *
                                     MathUtils.Sin(2f * ((float)Math.PI * MathUtils.Max(tailPhase - 0.25f, 0f))) -
                                     _tailTurn.X,
                                     -1f,
                                     1f
                                 );
                firstTailPitch = MathUtils.DegToRad(25f) *
                                 MathUtils.Clamp(
                                     0.5f * MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase) - _tailTurn.Y,
                                     -1f,
                                     1f
                                 );
                secondTailPitch = MathUtils.DegToRad(30f) *
                                  MathUtils.Clamp(
                                      0.5f *
                                      MathUtils.Sin(
                                          (float)Math.PI *
                                          2f *
                                          MathUtils.Max(MovementAnimationPhase - 0.25f, 0f)
                                      ) -
                                      _tailTurn.Y,
                                      -1f,
                                      1f
                                  );
            }
            else
            {
                firstTailRoll = MathUtils.DegToRad(25f) *
                                MathUtils.Clamp(
                                    0.5f * MathUtils.Sin((float)Math.PI * 2f * (MovementAnimationPhase + tailPhase)) -
                                    _tailTurn.X,
                                    -1f,
                                    1f
                                );
                secondTailRoll = MathUtils.DegToRad(30f) *
                                 MathUtils.Clamp(
                                     0.5f *
                                     MathUtils.Sin(
                                         2f *
                                         ((float)Math.PI *
                                          MathUtils.Max(
                                              MovementAnimationPhase + tailPhase - 0.25f,
                                              0f
                                          ))
                                     ) -
                                     _tailTurn.X,
                                     -1f,
                                     1f
                                 );
                firstTailPitch = MathUtils.DegToRad(25f) * MathUtils.Clamp(0f - _tailTurn.Y, -1f, 1f);
                secondTailPitch = MathUtils.DegToRad(30f) * MathUtils.Clamp(0f - _tailTurn.Y, -1f, 1f);
            }

            var radians = 0f;
            if (creaturePose.AttackPhase > 0f)
            {
                radians = (0f - MathUtils.DegToRad(30f)) * MathUtils.Sin((float)Math.PI * creaturePose.AttackPhase);
            }

            var value = Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) *
                        Matrix.CreateTranslation(
                            ComponentCreature.ComponentBody.Position +
                            new Vector3(0f, 0f - _digInDepth, 0f)
                        );
            SetNodeTransform(_bodyNode.Index, value);
            var identity = Matrix.Identity;
            if (firstTailRoll != 0f)
            {
                identity *= Matrix.CreateRotationZ(firstTailRoll);
            }

            if (firstTailPitch != 0f)
            {
                identity *= Matrix.CreateRotationX(firstTailPitch);
            }

            var identity2 = Matrix.Identity;
            if (secondTailRoll != 0f)
            {
                identity2 *= Matrix.CreateRotationZ(secondTailRoll);
            }

            if (secondTailPitch != 0f)
            {
                identity2 *= Matrix.CreateRotationX(secondTailPitch);
            }

            SetNodeTransform(_tail1Node.Index, identity);
            SetNodeTransform(_tail2Node.Index, identity2);
            if (_jawNode != null)
            {
                SetNodeTransform(_jawNode.Index, Matrix.CreateRotationX(radians));
            }
        }
        else
        {
            var bodyHeight = ComponentCreature.ComponentBody.BoundingBox.Max.Y -
                             ComponentCreature.ComponentBody.BoundingBox.Min.Y;
            var position = ComponentCreature.ComponentBody.Position + 1f * bodyHeight * DeathPhase * Vector3.UnitY;
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, 0f, (float)Math.PI * DeathPhase) *
                Matrix.CreateTranslation(position)
            );
            SetNodeTransform(_tail1Node.Index, Matrix.Identity);
            SetNodeTransform(_tail2Node.Index, Matrix.Identity);
            if (_jawNode != null)
            {
                SetNodeTransform(_jawNode.Index, Matrix.Identity);
            }
        }

        base.Animate();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        base.Load(valuesDictionary, idToEntityMap);
        _hasVerticalTail = valuesDictionary.GetValue<bool>("HasVerticalTail");
        _swimAnimationSpeed = valuesDictionary.GetValue<float>("SwimAnimationSpeed");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyNode = Model.FindNode("Body")!;
        _tail1Node = Model.FindNode("Tail1")!;
        _tail2Node = Model.FindNode("Tail2")!;
        _jawNode = Model.FindNode("Jaw", false);
    }
}
