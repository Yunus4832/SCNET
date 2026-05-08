using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentSummonBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private bool _isEnabled;

    private readonly Random _random = new();

    private double _stoppedTime;

    private SubsystemTime _subsystemTime = null!;

    private double _summonedTime;

    public ComponentBody? SummonTarget { get; set; }

    public bool IsEnabled => _isEnabled;

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
        _isEnabled = valuesDictionary.GetValue<bool>("IsEnabled");
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                SummonTarget = null;
                _summonedTime = 0.0;
            },
            delegate
            {
                if (_isEnabled && SummonTarget != null && _summonedTime == 0.0)
                {
                    _subsystemTime.QueueGameTimeDelayedExecution(_subsystemTime.GameTime + 0.5, delegate
                    {
                        _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                        _importanceLevel = 270f;
                        _summonedTime = _subsystemTime.GameTime;
                    });
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("FollowTarget");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "FollowTarget",
            delegate { FollowTarget(true); },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (SummonTarget == null || _componentPathfinding.IsStuck ||
                         _subsystemTime.GameTime - _summonedTime > 30.0)
                {
                    _importanceLevel = 0f;
                }
                else if (!_componentPathfinding.Destination.HasValue)
                {
                    if (_stoppedTime < 0.0)
                    {
                        _stoppedTime = _subsystemTime.GameTime;
                    }

                    if (_subsystemTime.GameTime - _stoppedTime > 6.0)
                    {
                        _importanceLevel = 0f;
                    }
                }

                FollowTarget(false);
                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    private void FollowTarget(bool noDelay)
    {
        if (SummonTarget == null || (!noDelay && !(_random.Float(0f, 1f) < 5f * _subsystemTime.GameTimeDelta)))
        {
            return;
        }

        var num = Vector3.Distance(_componentCreature.ComponentBody.Position, SummonTarget.Position);
        if (!(num > 4f))
        {
            return;
        }

        var v = Vector3.Normalize(Vector3.Cross(Vector3.UnitY,
            SummonTarget.Position - _componentCreature.ComponentBody.Position));
        v *= 0.75f * (GetHashCode() % 2 != 0 ? 1 : -1) * (1 + GetHashCode() % 3);
        var speed = MathUtils.Lerp(0.4f, 1f, MathUtils.Saturate(0.25f * (num - 5f)));
        _componentPathfinding.SetDestination(SummonTarget.Position + v, speed, 3.75f, 2000, true, false, true,
            null);
        _stoppedTime = -1.0;
    }
}
