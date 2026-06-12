using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFindPlayerBehavior : ComponentBehavior, IUpdateable
{
    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _dayRange;

    private float _dt;

    private float _importanceLevel;

    private float _minRange;

    private double _nextUpdateTime;

    private float _nightRange;

    private readonly Random _random = new();

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTime _subsystemTime = null!;

    private ComponentCreature? _target;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!(_subsystemTime.GameTime >= _nextUpdateTime))
        {
            return;
        }

        _dt = _random.Float(1.25f, 1.75f) +
              MathUtils.Min((float)(_subsystemTime.GameTime - _nextUpdateTime), 0.1f);
        _nextUpdateTime = _subsystemTime.GameTime + _dt;
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _dayRange = valuesDictionary.GetValue<float>("DayRange");
        _nightRange = valuesDictionary.GetValue<float>("NightRange");
        _minRange = valuesDictionary.GetValue<float>("MinRange");
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

                if (_subsystemGameInfo.WorldSettings.GameMode <= GameMode.Harmless)
                {
                    return;
                }

                _target = FindTarget();
                if (_target != null)
                {
                    var componentPlayer = _target.Entity.FindComponent<ComponentPlayer>();
                    if (componentPlayer is { ComponentSleep.IsSleeping: true })
                    {
                        _importanceLevel = 5f;
                    }
                    else if (_random.Float(0f, 1f) < 0.05f * _dt)
                    {
                        _importanceLevel = _random.Float(1f, 4f);
                    }
                }
                else
                {
                    _importanceLevel = 0f;
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                if (_target != null)
                {
                    _componentPathfinding.SetDestination(_target.ComponentBody.Position, _random.Float(0.5f, 0.7f),
                        _minRange, 500, true, true, false, null);
                }
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_target == null || _componentPathfinding.IsStuck ||
                         !_componentPathfinding.Destination.HasValue ||
                         ScoreTarget(_target) <= 0f)
                {
                    _importanceLevel = 0f;
                }

                if (_random.Float(0f, 1f) < 0.1f * _dt)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public ComponentCreature? FindTarget()
    {
        var position = _componentCreature.ComponentBody.Position;
        ComponentCreature? result = null;
        var num = 0f;
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z),
            MathUtils.Max(_nightRange, _dayRange), _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentCreature = _componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
            if (componentCreature == null)
            {
                continue;
            }

            var num2 = ScoreTarget(componentCreature);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            result = componentCreature;
        }

        return result;
    }

    public float ScoreTarget(ComponentCreature target)
    {
        var num = _subsystemSky.SkyLightIntensity > 0.2f ? _dayRange : _nightRange;
        if (!target.IsAddedToProject || target.ComponentHealth.Health <= 0f ||
            target.Entity.FindComponent<ComponentPlayer>() == null)
        {
            return 0f;
        }

        var num2 = Vector3.DistanceSquared(target.ComponentBody.Position, _componentCreature.ComponentBody.Position);
        if (num2 < _minRange * _minRange)
        {
            return 0f;
        }

        var score = num * num - num2;
        if (score <= 0f)
        {
            return score;
        }

        var context = new Game.Modding.CreatureTargetScoringContext(
            _componentCreature,
            target,
            Game.Modding.CreatureTargetingKind.FindPlayer,
            score);
        CurrentModRuntime.Value?.Gameplay.Invoke(context);
        return context.Cancel ? 0f : context.Score;
    }
}
