using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentAvoidFireBehavior : ComponentBehavior, IUpdateable
{
    private static float _circlingDirection = 1f;

    private float _dayRange;

    private double _ignoreFireUntil;

    private float _importanceLevel;

    private float _nightRange;

    private float _periodicEventOffset;

    private readonly Random _random = new();

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private SubsystemCampfireBlockBehavior _subsystemCampfireBlockBehavior = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTime _subsystemTime = null!;

    private Vector3? _target;

    public override float ImportanceLevel => _importanceLevel;

    public override string DebugInfo => !(_ignoreFireUntil < _subsystemTime.GameTime)
        ? string.Empty
        : $"ifu={_ignoreFireUntil - _subsystemTime.GameTime:0}";

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _subsystemCampfireBlockBehavior = Project.FindSubsystem<SubsystemCampfireBlockBehavior>(true)!;
        _dayRange = valuesDictionary.GetValue<float>("DayRange");
        _nightRange = valuesDictionary.GetValue<float>("NightRange");
        _periodicEventOffset = _random.Float(0f, 10f);
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
                    stateMachine.TransitionTo(_importanceLevel < 10f ? "Circle" : "Move");
                }
                else if (_subsystemTime.PeriodicGameTimeEvent(1.0, _periodicEventOffset))
                {
                    _target = FindTarget(out var targetScore);
                    if (_target.HasValue)
                    {
                        if (_random.Float(0f, 1f) < 0.015f)
                        {
                            _ignoreFireUntil = _subsystemTime.GameTime + 20.0;
                        }

                        Vector3.Distance(_target.Value, _componentCreature.ComponentBody.Position);
                        _importanceLevel = _subsystemTime.GameTime < _ignoreFireUntil ? 0f :
                            targetScore > 0.5f ? 250f : _random.Float(1f, 5f);
                    }
                    else
                    {
                        _importanceLevel = 0f;
                    }
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                if (!_target.HasValue)
                {
                    return;
                }

                var vector2 =
                    Vector3.Normalize(
                        Vector3.Normalize(_componentCreature.ComponentBody.Position - _target.Value) +
                        _random.Vector3(0.5f));
                var value2 = _componentCreature.ComponentBody.Position + _random.Float(6f, 8f) *
                    Vector3.Normalize(new Vector3(vector2.X, 0f, vector2.Z));
                _componentPathfinding.SetDestination(value2, _random.Float(0.6f, 0.8f), 1f, 0, false, true, false,
                    null);
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (!_target.HasValue || _componentPathfinding.IsStuck ||
                         !_componentPathfinding.Destination.HasValue ||
                         ScoreTarget(_target.Value) <= 0f)
                {
                    _importanceLevel = 0f;
                }

                if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Circle",
            delegate
            {
                if (!_target.HasValue)
                {
                    return;
                }

                var vector =
                    Vector3.Cross(Vector3.Normalize(_componentCreature.ComponentBody.Position - _target.Value),
                        Vector3.UnitY) * _circlingDirection;
                var value = _componentCreature.ComponentBody.Position +
                            _random.Float(6f, 8f) * Vector3.Normalize(new Vector3(vector.X, 0f, vector.Z));
                _componentPathfinding.SetDestination(value, _random.Float(0.4f, 0.9f), 1f, 0, false, true, false, null);
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_componentPathfinding.IsStuck)
                {
                    _circlingDirection = 0f - _circlingDirection;
                    _importanceLevel = 0f;
                }
                else if (!_target.HasValue || !_componentPathfinding.Destination.HasValue ||
                         ScoreTarget(_target.Value) <= 0f)
                {
                    _importanceLevel = 0f;
                }

                if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                _componentCreature.ComponentCreatureModel.LookAtOrder = _target;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public Vector3? FindTarget(out float targetScore)
    {
        _ = _componentCreature.ComponentBody.Position;
        Vector3? result = null;
        var num = 0f;
        foreach (var campfire in _subsystemCampfireBlockBehavior.Campfires)
        {
            var vector = new Vector3(campfire.X, campfire.Y, campfire.Z);
            var num2 = ScoreTarget(vector);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            result = vector;
        }

        targetScore = num;
        return result;
    }

    public float ScoreTarget(Vector3 target)
    {
        var num = _subsystemSky.SkyLightIntensity > 0.2f ? _dayRange : _nightRange;
        if (!(num > 0f))
        {
            return 0f;
        }

        var num2 = Vector3.Distance(target, _componentCreature.ComponentBody.Position);
        return MathUtils.Saturate(1f - num2 / num);
    }
}
