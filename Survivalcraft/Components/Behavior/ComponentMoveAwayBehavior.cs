using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentMoveAwayBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private bool _isFast;

    private readonly Random _random = new();

    private ComponentBody? _target;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentCreature.ComponentBody.CollidedWithBody += delegate(ComponentBody body)
        {
            _target = body;
            _isFast = MathUtils.Max(body.Velocity.Length(), _componentCreature.ComponentBody.Velocity.Length()) > 3f;
        };
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                _target = null;
            },
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Move");
                }

                if (_target != null)
                {
                    _importanceLevel = 6f;
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                if (_random.Float(0f, 1f) < 0.5f)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                if (_target == null)
                {
                    return;
                }

                var vector = _target.Position + 0.5f * _target.Velocity;
                var v = Vector2.Normalize(_componentCreature.ComponentBody.Position.XZ - vector.XZ);
                var vector2 = Vector2.Zero;
                var num = float.MinValue;
                for (var num2 = 0f; num2 < (float)Math.PI * 2f; num2 += 0.1f)
                {
                    var vector3 = Vector2.CreateFromAngle(num2);
                    if (!(Vector2.Dot(vector3, v) > 0.2f))
                    {
                        continue;
                    }

                    var num3 = Vector2.Dot(_componentCreature.ComponentBody.Matrix.Forward.XZ, vector3);
                    if (!(num3 > num))
                    {
                        continue;
                    }

                    vector2 = vector3;
                    num = num3;
                }

                var s = _random.Float(1.5f, 2f);
                var speed = _isFast ? 0.7f : 0.35f;
                _componentPathfinding.SetDestination(
                    _componentCreature.ComponentBody.Position + s * new Vector3(vector2.X, 0f, vector2.Y), speed,
                    1f,
                    0, false, true, false, null);
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_componentPathfinding.IsStuck || !_componentPathfinding.Destination.HasValue)
                {
                    _importanceLevel = 0f;
                }

                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }
}
