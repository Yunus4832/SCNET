using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Components;

public class ComponentSteedBehavior : ComponentBehavior, IUpdateable
{
    private readonly DynamicArray<ComponentBody> _bodies = [];

    private ComponentCreature _componentCreature = null!;

    private ComponentMount _componentMount = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private bool _isEnabled;

    private double _lastNotBlockedTime;

    private readonly Random _random = new();

    private float _speed;

    private float _speedChangeFactor;

    private int _speedLevel;

    private readonly float[] _speedLevels =
    [
        -0.33f,
        0f,
        0.33f,
        0.66f,
        1f
    ];

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _timeToSpeedReduction;

    private float _turnSpeed;

    public int SpeedOrder { get; set; }

    public float TurnOrder { get; set; }

    public float JumpOrder { get; set; }

    public bool WasOrderIssued { get; set; }

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
        if (SpeedOrder != 0 || TurnOrder != 0f || JumpOrder != 0f)
        {
            SpeedOrder = 0;
            TurnOrder = 0f;
            JumpOrder = 0f;
            WasOrderIssued = true;
        }
        else
        {
            WasOrderIssued = false;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(1.0, GetHashCode() % 100 * 0.01f))
        {
            _importanceLevel = 0f;
            if (_isEnabled)
            {
                if (_componentMount.Rider != null)
                {
                    _importanceLevel = 275f;
                }
                else if (FindNearbyRider(7f) != null)
                {
                    _importanceLevel = 7f;
                }
            }
        }

        if (!IsActive)
        {
            stateMachine.TransitionTo("Inactive");
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentMount = Entity.FindComponent<ComponentMount>(true)!;
        _isEnabled = Entity.ValuesDictionary.DatabaseObject.Name.EndsWith("_Saddled");
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Wait");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Wait",
            delegate
            {
                var componentRider = FindNearbyRider(6f);
                if (componentRider == null)
                {
                    return;
                }

                _componentPathfinding.SetDestination(componentRider.ComponentCreature.ComponentBody.Position,
                    _random.Float(0.2f, 0.3f), 3.25f, 0, false, true, false, null);
                if (_random.Float(0f, 1f) < 0.5f)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }
            }, delegate
            {
                if (_componentMount.Rider != null)
                {
                    stateMachine.TransitionTo("Steed");
                }

                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Steed",
            delegate
            {
                _componentPathfinding.Stop();
                _speed = 0f;
                _speedLevel = 1;
            },
            ProcessRidingOrders,
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public ComponentRider? FindNearbyRider(float range)
    {
        _bodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(
            new Vector2(_componentCreature.ComponentBody.Position.X, _componentCreature.ComponentBody.Position.Z),
            range, _bodies);
        foreach (var body in _bodies)
        {
            if (!(Vector3.DistanceSquared(_componentCreature.ComponentBody.Position, body.Position) <
                  range * range))
            {
                continue;
            }

            var componentRider = body.Entity.FindComponent<ComponentRider>();
            if (componentRider != null)
            {
                return componentRider;
            }
        }

        return null;
    }

    private void ProcessRidingOrders()
    {
        if (CommonLib.WorkType != WorkType.Local)
        {
            if (_componentCreature.ComponentBody.ChildBodies.Count > 0)
            {
                //被骑乘
                var body = _componentCreature.ComponentBody.ChildBodies[0];
                if (body.Player is { PlayerData.IsMainPlayer: false })
                {
                    return;
                }
            }
        }

        _speedLevel = MathUtils.Clamp(_speedLevel + SpeedOrder, 0, _speedLevels.Length - 1);
        if (_speedLevel == _speedLevels.Length - 1 && SpeedOrder > 0)
        {
            _timeToSpeedReduction = _random.Float(8f, 12f);
        }

        if (_speedLevel == 0 && SpeedOrder < 0)
        {
            _timeToSpeedReduction = 1.25f;
        }

        _timeToSpeedReduction -= _subsystemTime.GameTimeDelta;
        switch (_timeToSpeedReduction)
        {
            case <= 0f when _speedLevel == _speedLevels.Length - 1:
                _speedLevel--;
                _speedChangeFactor = 0.25f;
                break;
            case <= 0f when _speedLevel == 0:
                _speedLevel = 1;
                _speedChangeFactor = 100f;
                break;
            default:
                _speedChangeFactor = 100f;
                break;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(0.25, 0.0))
        {
            var num = new Vector2(_componentCreature.ComponentBody.CollisionVelocityChange.X,
                _componentCreature.ComponentBody.CollisionVelocityChange.Z).Length();
            if (_speedLevel == 0 || num < 0.1f || _componentCreature.ComponentBody.Velocity.Length() >
                MathUtils.Abs(0.5f * _speed * _componentCreature.ComponentLocomotion.WalkSpeed))
            {
                _lastNotBlockedTime = _subsystemTime.GameTime;
            }
            else if (_subsystemTime.GameTime - _lastNotBlockedTime > 0.75)
            {
                _speedLevel = 1;
            }
        }

        _speed += MathUtils.Saturate(_speedChangeFactor * _subsystemTime.GameTimeDelta) *
                  (_speedLevels[_speedLevel] - _speed);
        _turnSpeed += 2f * _subsystemTime.GameTimeDelta * (MathUtils.Clamp(TurnOrder, -0.5f, 0.5f) - _turnSpeed);
        _componentCreature.ComponentLocomotion.TurnOrder = new Vector2(_turnSpeed, 0f);
        _componentCreature.ComponentLocomotion.WalkOrder = new Vector2(0f, _speed);
        if (MathUtils.Abs(_speed) > 0.01f || MathUtils.Abs(_turnSpeed) > 0.01f)
        {
            _componentCreature.ComponentLocomotion.LookOrder = new Vector2(2f * _turnSpeed, 0f) -
                                                               _componentCreature.ComponentLocomotion.LookAngles;
        }

        _componentCreature.ComponentLocomotion.JumpOrder =
            MathUtils.Max(_componentCreature.ComponentLocomotion.JumpOrder, JumpOrder);
    }
}
