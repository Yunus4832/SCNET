using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentRandomPeckBehavior : ComponentBehavior, IUpdateable
{
    private ComponentBirdModel _componentBirdModel = null!;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _dt;

    private float _importanceLevel = 1f;

    private bool _isFeed;

    private float _peckTime;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private float _waitTime;

    public override float ImportanceLevel => _importanceLevel;

    public bool IsFeed
    {
        get => _isFeed;
        set
        {
            if (_isFeed == value)
            {
                return;
            }

            _isFeed = value;
            if (CommonLib.WorkType == WorkType.Server)
            {
                CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, _isFeed));
            }
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            _componentCreature.ComponentCreatureModel.FeedOrder = IsFeed;
        }
        else
        {
            if (string.IsNullOrEmpty(stateMachine.CurrentState))
            {
                stateMachine.TransitionTo("Move");
            }

            if (_random.Float(0f, 1f) < 0.033f * dt)
            {
                _importanceLevel = _random.Float(1f, 2.5f);
            }

            _dt = dt;
            if (IsActive)
            {
                stateMachine.Update();
            }
            else
            {
                stateMachine.TransitionTo("Inactive");
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentBirdModel = Entity.FindComponent<ComponentBirdModel>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Move");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stuck",
            delegate { stateMachine.TransitionTo("Move"); },
            Actions.Empty,
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                var position = _componentCreature.ComponentBody.Position;
                var num = _random.Float(0f, 1f) < 0.2f ? 8f : 3f;
                var value = position + new Vector3(num * _random.Float(-1f, 1f), 0f, num * _random.Float(-1f, 1f));
                value.Y = _subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(value.X), Terrain.ToCell(value.Z)) + 1;
                _componentPathfinding.SetDestination(value, _random.Float(0.5f, 0.7f), 1f, 0, false, true, false, null);
            },
            delegate
            {
                if (!_componentPathfinding.Destination.HasValue)
                {
                    stateMachine.TransitionTo(_random.Float(0f, 1f) < 0.33f ? "Wait" : "Peck");
                }
                else if (_componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("Stuck");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Wait",
            delegate { _waitTime = _random.Float(0.75f, 1f); },
            delegate
            {
                _waitTime -= _dt;
                if (!(_waitTime <= 0f))
                {
                    return;
                }

                if (_random.Float(0f, 1f) < 0.25f)
                {
                    stateMachine.TransitionTo("Move");
                    if (_random.Float(0f, 1f) < 0.33f)
                    {
                        _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                    }
                }
                else
                {
                    stateMachine.TransitionTo("Peck");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Peck",
            delegate { _peckTime = _random.Float(2f, 6f); },
            delegate
            {
                _peckTime -= _dt;
                if (_componentCreature.ComponentBody.StandingOnValue.HasValue)
                {
                    _componentBirdModel.FeedOrder = true;
                    IsFeed = true;
                }
                else
                {
                    IsFeed = false;
                }

                if (!(_peckTime <= 0f))
                {
                    return;
                }

                stateMachine.TransitionTo(_random.Float(0f, 1f) < 0.25f ? "Move" : "Wait");
            },
            delegate { IsFeed = false; }
        );
    }
}
