using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentHerdBehavior : ComponentBehavior, IUpdateable
{
    private bool _autoNearbyCreaturesHelp;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _dt;

    private float _herdingRange;

    private float _importanceLevel;

    private Vector2 _look;

    private readonly Random _random = new();

    private SubsystemCreatureSpawn _subsystemCreatureSpawn = null!;

    private SubsystemTime _subsystemTime = null!;

    public string HerdName { get; set; } = string.Empty;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (string.IsNullOrEmpty(stateMachine.CurrentState) || !IsActive)
        {
            stateMachine.TransitionTo("Inactive");
        }

        _dt = dt;
        stateMachine.Update();
    }

    public void CallNearbyCreaturesHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
    {
        var position = target.ComponentBody.Position;
        foreach (var creature in _subsystemCreatureSpawn.Creatures)
        {
            if (!(Vector3.DistanceSquared(position, creature.ComponentBody.Position) < 512f))
            {
                continue;
            }

            var componentHerdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();
            if (componentHerdBehavior == null || componentHerdBehavior.HerdName != HerdName ||
                !componentHerdBehavior._autoNearbyCreaturesHelp)
            {
                continue;
            }

            var componentChaseBehavior = creature.Entity.FindComponent<ComponentChaseBehavior>();
            if (componentChaseBehavior is { Target: null })
            {
                componentChaseBehavior.Attack(target, maxRange, maxChaseTime, isPersistent);
            }
        }
    }

    public Vector3? FindHerdCenter()
    {
        if (string.IsNullOrEmpty(HerdName))
        {
            return null;
        }

        var position = _componentCreature.ComponentBody.Position;
        var num = 0;
        var zero = Vector3.Zero;
        foreach (var creature in _subsystemCreatureSpawn.Creatures)
        {
            if (!(creature.ComponentHealth.Health > 0f))
            {
                continue;
            }

            var componentHerdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();
            if (componentHerdBehavior == null || componentHerdBehavior.HerdName != HerdName)
            {
                continue;
            }

            var position2 = creature.ComponentBody.Position;
            if (!(Vector3.DistanceSquared(position, position2) < _herdingRange * _herdingRange))
            {
                continue;
            }

            zero += position2;
            num++;
        }

        if (num > 0)
        {
            return zero / num;
        }

        return null;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        HerdName = valuesDictionary.GetValue<string>("HerdName");
        _herdingRange = valuesDictionary.GetValue<float>("HerdingRange");
        _autoNearbyCreaturesHelp = valuesDictionary.GetValue<bool>("AutoNearbyCreaturesHelp");
        _componentCreature.ComponentHealth.Attacked += delegate (ComponentCreature attacker)
        {
            CallNearbyCreaturesHelp(attacker, 20f, 30f, false);
        };
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (_subsystemTime.PeriodicGameTimeEvent(1.0, 1f * (GetHashCode() % 256 / 256f)))
                {
                    var vector2 = FindHerdCenter();
                    if (vector2.HasValue)
                    {
                        var num = Vector3.Distance(vector2.Value, _componentCreature.ComponentBody.Position);
                        if (num > 10f)
                        {
                            _importanceLevel = 1f;
                        }

                        if (num > 12f)
                        {
                            _importanceLevel = 3f;
                        }

                        if (num > 16f)
                        {
                            _importanceLevel = 50f;
                        }

                        if (num > 20f)
                        {
                            _importanceLevel = 250f;
                        }
                    }
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Herd");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stuck",
            delegate
            {
                stateMachine.TransitionTo("Herd");
                if (!_random.Bool(0.5f))
                {
                    return;
                }

                _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                _importanceLevel = 0f;
            },
            Actions.Empty,
            Actions.Empty
        );
        stateMachine.AddState(
            "Herd",
            delegate
            {
                var vector = FindHerdCenter();
                if (vector.HasValue && Vector3.Distance(_componentCreature.ComponentBody.Position, vector.Value) > 6f)
                {
                    var speed = _importanceLevel > 10f ? _random.Float(0.9f, 1f) : _random.Float(0.25f, 0.35f);
                    var maxPathfindingPositions = _importanceLevel > 200f ? 100 : 0;
                    _componentPathfinding.SetDestination(vector.Value, speed, 7f, maxPathfindingPositions, false, true,
                        false, null);
                }
                else
                {
                    _importanceLevel = 0f;
                }
            },
            delegate
            {
                _componentCreature.ComponentLocomotion.LookOrder =
                    _look - _componentCreature.ComponentLocomotion.LookAngles;
                if (_componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("Stuck");
                }

                if (!_componentPathfinding.Destination.HasValue)
                {
                    _importanceLevel = 0f;
                }

                if (_random.Float(0f, 1f) < 0.05f * _dt)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                }

                if (_random.Float(0f, 1f) < 1.5f * _dt)
                {
                    _look = new Vector2(MathUtils.DegToRad(45f) * _random.Float(-1f, 1f),
                        MathUtils.DegToRad(10f) * _random.Float(-1f, 1f));
                }
            },
            Actions.Empty
        );
    }
}
