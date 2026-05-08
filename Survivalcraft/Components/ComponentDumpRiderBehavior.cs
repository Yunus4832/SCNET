using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Components;

public class ComponentDumpRiderBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentMount _componentMount = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private double _dumpStartTime;

    private float _importanceLevel;

    private bool _isEnabled;

    private Vector2 _lookOrder;

    private readonly Random _random = new();

    private ComponentRider? _rider;

    private SubsystemTime _subsystemTime = null!;

    private Vector2 _turnOrder;

    private Vector2 _walkOrder;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentMount = Entity.FindComponent<ComponentMount>(true)!;
        _isEnabled = !Entity.ValuesDictionary.DatabaseObject.Name.EndsWith("_Saddled");
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                _rider = null;
            },
            delegate
            {
                if (_isEnabled && _random.Float(0f, 1f) < 1f * _subsystemTime.GameTimeDelta &&
                    _componentMount.Rider != null)
                {
                    _importanceLevel = 220f;
                    _dumpStartTime = _subsystemTime.GameTime;
                    _rider = _componentMount.Rider;
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("WildJumping");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "WildJumping",
            delegate
            {
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
                _componentPathfinding.Stop();
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_componentMount.Rider == null)
                {
                    _importanceLevel = 0f;
                    RunAway();
                }

                if (_random.Float(0f, 1f) < 1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayPainSound();
                }

                if (_random.Float(0f, 1f) < 3f * _subsystemTime.GameTimeDelta)
                {
                    _walkOrder = new Vector2(_random.Float(-0.5f, 0.5f), _random.Float(-0.5f, 1.5f));
                }

                if (_random.Float(0f, 1f) < 2.5f * _subsystemTime.GameTimeDelta)
                {
                    _turnOrder.X = _random.Float(-1f, 1f);
                }

                if (_random.Float(0f, 1f) < 2f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentLocomotion.JumpOrder = _random.Float(0.9f, 1f);
                    if (_componentMount.Rider != null && _subsystemTime.GameTime - _dumpStartTime > 3.0)
                    {
                        if (_random.Float(0f, 1f) < 0.05f)
                        {
                            _componentMount.Rider.StartDismounting();
                            if (CommonLib.WorkType != WorkType.Client)
                            {
                                _componentMount.Rider.ComponentCreature.ComponentHealth.Injure(
                                    _random.Float(0.05f, 0.2f),
                                    _componentCreature, false, "Thrown from a mount");
                            }
                        }

                        if (_random.Float(0f, 1f) < 0.25f)
                        {
                            if (CommonLib.WorkType != WorkType.Client)
                            {
                                _componentMount.Rider.ComponentCreature.ComponentHealth.Injure(0.05f,
                                    _componentCreature,
                                    false, "Thrown from a mount");
                            }
                        }
                    }
                }

                if (_random.Float(0f, 1f) < 4f * _subsystemTime.GameTimeDelta)
                {
                    _lookOrder = new Vector2(_random.Float(-3f, 3f), _lookOrder.Y);
                }

                if (_random.Float(0f, 1f) < 0.25f * _subsystemTime.GameTimeDelta)
                {
                    TransitionToRandomDumpingBehavior();
                }

                _componentCreature.ComponentLocomotion.WalkOrder = _walkOrder;
                _componentCreature.ComponentLocomotion.TurnOrder = _turnOrder;
                _componentCreature.ComponentLocomotion.LookOrder = _lookOrder;
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "BlindRacing",
            delegate
            {
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
                _componentPathfinding.SetDestination(
                    _componentCreature.ComponentBody.Position +
                    new Vector3(_random.Float(-15f, 15f), 0f, _random.Float(-15f, 15f)), 1f, 2f, 0, false, true, false,
                    null);
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_componentMount.Rider == null)
                {
                    _importanceLevel = 0f;
                    RunAway();
                }
                else if (!_componentPathfinding.Destination.HasValue || _componentPathfinding.IsStuck)
                {
                    TransitionToRandomDumpingBehavior();
                }

                if (!(_random.Float(0f, 1f) < 0.5f * _subsystemTime.GameTimeDelta))
                {
                    return;
                }

                _componentCreature.ComponentLocomotion.JumpOrder = 1f;
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stupor",
            delegate
            {
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
                _componentPathfinding.Stop();
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_componentMount.Rider == null)
                {
                    _importanceLevel = 0f;
                }

                if (_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
                {
                    TransitionToRandomDumpingBehavior();
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public void TransitionToRandomDumpingBehavior()
    {
        var num = _random.Float(0f, 1f);
        switch (num)
        {
            case < 0.5f:
                stateMachine.TransitionTo("WildJumping");
                break;
            case < 0.8f:
                stateMachine.TransitionTo("BlindRacing");
                break;
            default:
                stateMachine.TransitionTo("Stupor");
                break;
        }
    }

    public void RunAway()
    {
        if (_rider == null)
        {
            return;
        }

        Entity.FindComponent<ComponentRunAwayBehavior>()?.RunAwayFrom(_rider.ComponentCreature.ComponentBody);
    }
}
