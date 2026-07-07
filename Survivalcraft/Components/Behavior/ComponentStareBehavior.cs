using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentStareBehavior : ComponentBehavior, IUpdateable
{
    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private readonly Random _random = new();

    private double _stareEndTime;

    private float _stareRange;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemTime _subsystemTime = null!;

    private ComponentCreature? _target;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _stareRange = valuesDictionary.GetValue<float>("StareRange");
        stateMachine.AddState(
            "Inactive",
            delegate { _importanceLevel = 0f; },
            delegate
            {
                if (_subsystemTime.GameTime > _stareEndTime + 8.0 &&
                    _random.Float(0f, 1f) < 1f * _subsystemTime.GameTimeDelta)
                {
                    _target = FindTarget();
                    if (_target != null)
                    {
                        var probability = _target.Entity.FindComponent<ComponentPlayer>() != null ? 1f : 0.25f;
                        if (_random.Bool(probability))
                        {
                            _importanceLevel = _random.Float(3f, 5f);
                        }
                    }
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Stare");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stare",
            delegate
            {
                _stareEndTime = _subsystemTime.GameTime + _random.Float(6f, 12f);
                if (_target == null)
                {
                    return;
                }

                var position = _componentCreature.ComponentBody.Position;
                var v = Vector3.Normalize(_target.ComponentBody.Position - position);
                _componentPathfinding.SetDestination(position + 1.1f * v, _random.Float(0.3f, 0.4f), 1f, 0, false,
                    true, false, null);
                if (_random.Float(0f, 1f) < 0.5f)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                }
            },
            delegate
            {
                if (!IsActive || _target == null || _componentPathfinding.IsStuck ||
                    _subsystemTime.GameTime > _stareEndTime ||
                    _random.Float(0f, 1f) < 1f * _subsystemTime.GameTimeDelta && ScoreTarget(_target) <= 0f)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else
                {
                    _componentCreature.ComponentCreatureModel.LookAtOrder = _target.ComponentCreatureModel.EyePosition;
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public ComponentCreature? FindTarget()
    {
        var position = _componentCreature.ComponentBody.Position;
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), _stareRange, _componentBodies);
        ComponentCreature? result = null;
        var num = 0f;
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

            result = componentCreature;
            num = num2;
        }

        return result;
    }

    public float ScoreTarget(ComponentCreature componentCreature)
    {
        if (componentCreature == _componentCreature || !componentCreature.Entity.IsAddedToProject)
        {
            return 0f;
        }

        var num = Vector3.Distance(_componentCreature.ComponentBody.Position,
            componentCreature.ComponentBody.Position);
        var num2 = _stareRange - num;
        if (_random.Float(0f, 1f) < 0.66f &&
            componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
        {
            num2 *= 100f;
        }

        if (num2 <= 0f)
        {
            return num2;
        }

        var context = new Game.Modding.CreatureTargetScoringContext(
            _componentCreature,
            componentCreature,
            Game.Modding.CreatureTargetingKind.Stare,
            num2);
        CurrentModRuntime.Value?.Gameplay.Invoke(context);
        return context.Cancel ? 0f : context.Score;
    }
}
