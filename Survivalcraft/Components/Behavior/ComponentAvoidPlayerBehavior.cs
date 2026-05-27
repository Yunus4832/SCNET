using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentAvoidPlayerBehavior : ComponentBehavior, IUpdateable
{
    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private float _dayRange;

    private float _dt;

    private float _importanceLevel;

    private double _nextUpdateTime;

    private float _nightRange;

    private readonly Random _random = new();

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private SubsystemBodies _subsystemBodies = null!;

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

        _dt = _random.Float(0.4f, 0.6f) +
              MathUtils.Min((float)(_subsystemTime.GameTime - _nextUpdateTime), 0.1f);
        _nextUpdateTime = _subsystemTime.GameTime + _dt;
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _dayRange = valuesDictionary.GetValue<float>("DayRange");
        _nightRange = valuesDictionary.GetValue<float>("NightRange");
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

                _target = FindTarget(out var targetScore);
                if (_target != null)
                {
                    Vector3.Distance(_target.ComponentBody.Position, _componentCreature.ComponentBody.Position);
                    SetImportanceLevel(targetScore);
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
            Actions.Empty,
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_target == null || _componentPathfinding.IsStuck ||
                         !_componentPathfinding.Destination.HasValue)
                {
                    _importanceLevel = 0f;
                }
                else
                {
                    var num = ScoreTarget(_target);
                    SetImportanceLevel(num);
                    var vector =
                        Vector3.Normalize(_componentCreature.ComponentBody.Position - _target.ComponentBody.Position);
                    var value = _componentCreature.ComponentBody.Position +
                                10f * Vector3.Normalize(new Vector3(vector.X, 0f, vector.Z));
                    _componentPathfinding.SetDestination(value, MathUtils.Lerp(0.6f, 1f, num), 1f, 0, false, true,
                        false,
                        null);
                    _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
                    if (_random.Float(0f, 1f) < 0.1f * _dt)
                    {
                        _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                    }
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public void SetImportanceLevel(float score)
    {
        _importanceLevel = MathUtils.Lerp(4f, 8f, MathUtils.Sqrt(score));
    }

    public ComponentCreature? FindTarget(out float targetScore)
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
            if (componentCreature is null)
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

        targetScore = num;
        return result;
    }

    public float ScoreTarget(ComponentCreature target)
    {
        var num = _subsystemSky.SkyLightIntensity > 0.2f ? _dayRange : _nightRange;
        if (!(num > 0f))
        {
            return 0f;
        }

        if (!target.IsAddedToProject || target.ComponentHealth.Health <= 0f ||
            target.Entity.FindComponent<ComponentPlayer>() == null)
        {
            return 0f;
        }

        var num2 = Vector3.Distance(target.ComponentBody.Position, _componentCreature.ComponentBody.Position);
        return MathUtils.Saturate(1f - num2 / num);
    }
}
