using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFlyAroundBehavior : ComponentBehavior, IUpdateable
{
    private float _angle;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel = 1f;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (string.IsNullOrEmpty(stateMachine.CurrentState))
        {
            stateMachine.TransitionTo("Inactive");
        }

        if (_random.Float(0f, 1f) < 0.05f * dt)
        {
            _importanceLevel = _random.Float(1f, 2f);
        }

        if (IsActive)
        {
            stateMachine.Update();
        }
        else
        {
            stateMachine.TransitionTo("Inactive");
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Fly");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stuck",
            delegate
            {
                stateMachine.TransitionTo("Fly");
                if (!(_random.Float(0f, 1f) < 0.5f))
                {
                    return;
                }

                _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                _importanceLevel = 1f;
            },
            Actions.Empty,
            Actions.Empty
        );
        stateMachine.AddState(
            "Fly",
            delegate
            {
                _angle = _random.Float(0f, (float)Math.PI * 2f);
                _componentPathfinding.Stop();
            },
            delegate
            {
                var position = _componentCreature.ComponentBody.Position;
                if (!_componentPathfinding.Destination.HasValue)
                {
                    var num = _random.Float(0f, 1f) < 0.2f ? _random.Float(0.4f, 0.6f) : 0f - _random.Float(0.4f, 0.6f);
                    _angle = MathUtils.NormalizeAngle(_angle + num);
                    var vector = Vector2.CreateFromAngle(_angle);
                    var value = position + new Vector3(vector.X, 0f, vector.Y) * 10f;
                    value.Y = EstimateHeight(new Vector2(value.X, value.Z), 8) + _random.Float(3f, 5f);
                    _componentPathfinding.SetDestination(value, _random.Float(0.6f, 1.05f), 6f, 0, false, true, false,
                        null);
                    if (_random.Float(0f, 1f) < 0.15f)
                    {
                        _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                    }
                }
                else if (_componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("Stuck");
                }
            }, Actions.Empty
        );
    }

    public float EstimateHeight(Vector2 position, int radius)
    {
        var num = 0;
        for (var i = 0; i < 15; i++)
        {
            var x = Terrain.ToCell(position.X) + _random.Int(-radius, radius);
            var z = Terrain.ToCell(position.Y) + _random.Int(-radius, radius);
            num = MathUtils.Max(num, _subsystemTerrain.Terrain.GetTopHeight(x, z));
        }

        return num;
    }
}
