using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentRandomFeedBehavior : ComponentBehavior, IUpdateable
{
    private bool _autoFeed;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private Vector3? _feedPosition;

    private float _feedTime;

    private float _importanceLevel;

    private bool _isFeed;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

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
            stateMachine.Update();
        }
    }

    public void Feed(Vector3 feedPosition)
    {
        _importanceLevel = 5f;
        _feedPosition = feedPosition;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _autoFeed = valuesDictionary.GetValue<bool>("AutoFeed");
        stateMachine.AddState(
            "Inactive",
            delegate { _importanceLevel = _random.Float(0f, 1f); },
            delegate
            {
                if (_random.Float(0f, 1f) < 0.05f * _subsystemTime.GameTimeDelta)
                {
                    _importanceLevel = _random.Float(1f, 3f);
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Move");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                Vector3 value;
                if (_feedPosition.HasValue)
                {
                    value = _feedPosition.Value;
                }
                else
                {
                    var position = _componentCreature.ComponentBody.Position;
                    var forward = _componentCreature.ComponentBody.Matrix.Forward;
                    var num4 = _random.Float(0f, 1f) < 0.2f ? 5f : 1.5f;
                    value = position + num4 * forward +
                            0.5f * num4 * new Vector3(_random.Float(-1f, 1f), 0f, _random.Float(-1f, 1f));
                }

                value.Y = _subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(value.X), Terrain.ToCell(value.Z)) + 1;
                _componentPathfinding.SetDestination(value, _random.Float(0.25f, 0.35f), 1f, 0, false, true, false,
                    null);
            },
            delegate
            {
                if (!_componentPathfinding.Destination.HasValue)
                {
                    var num3 = _random.Float(0f, 1f);
                    if (num3 < 0.33f)
                    {
                        stateMachine.TransitionTo("Inactive");
                    }
                    else if (num3 < 0.66f)
                    {
                        stateMachine.TransitionTo("LookAround");
                    }
                    else
                    {
                        stateMachine.TransitionTo("Feed");
                    }
                }
                else if (!IsActive || _componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("Inactive");
                }
            },
            delegate { _feedPosition = null; }
        );
        stateMachine.AddState(
            "LookAround",
            delegate { _waitTime = _random.Float(1f, 2f); },
            delegate
            {
                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
                _waitTime -= _subsystemTime.GameTimeDelta;
                if (_waitTime <= 0f)
                {
                    var num2 = _random.Float(0f, 1f);
                    if (num2 < 0.25f)
                    {
                        stateMachine.TransitionTo("Inactive");
                    }

                    switch (num2)
                    {
                        case < 0.5f:
                            stateMachine.TransitionTo(string.Empty);
                            stateMachine.TransitionTo("LookAround");
                            break;
                        case < 0.75f:
                        {
                            stateMachine.TransitionTo("Move");
                            if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                            {
                                _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                            }

                            break;
                        }
                        default:
                            stateMachine.TransitionTo("Feed");
                            break;
                    }
                }

                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Feed",
            delegate { _feedTime = _random.Float(4f, 6f); },
            delegate
            {
                _feedTime -= _subsystemTime.GameTimeDelta;
                if (_componentCreature.ComponentBody.StandingOnValue.HasValue)
                {
                    _componentCreature.ComponentCreatureModel.FeedOrder = true;
                    IsFeed = true;
                    if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                    {
                        _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                    }

                    if (_random.Float(0f, 1f) < 1.5f * _subsystemTime.GameTimeDelta)
                    {
                        _componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
                    }
                }

                if (_feedTime <= 0f)
                {
                    if (_autoFeed)
                    {
                        var num = _random.Float(0f, 1f);
                        if (num < 0.33f)
                        {
                            stateMachine.TransitionTo("Inactive");
                        }

                        stateMachine.TransitionTo(num < 0.66f ? "Move" : "LookAround");
                    }
                    else
                    {
                        _importanceLevel = 0f;
                    }
                }

                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
            },
            delegate { IsFeed = false; }
        );
        stateMachine.TransitionTo("Inactive");
    }
}
