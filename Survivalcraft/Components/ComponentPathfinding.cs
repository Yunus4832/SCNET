using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentPathfinding : Component, IUpdateable
{
    public const float MinPathfindingPeriod = 6f;

    public const float PathfindingCongestionCapacity = 500f;

    public const float PathfindingCongestionCapacityLimit = 1000f;

    public const float PathfindingCongestionDecayRate = 20f;

    public static bool RawPathfinding;

    private ComponentCreature _componentCreature = null!;

    private ComponentPilot _componentPilot = null!;

    private bool _destinationChanged;

    private Vector3? _lastPathfindingDestination;

    private double? _lastPathfindingTime;

    private double _nextUpdateTime;

    private float _pathfindingCongestion;

    private PathfindingResult _pathfindingResult = new();

    private readonly Random _random = new();

    private int _randomMoveCount;

    private readonly StateMachine _stateMachine = new();

    private SubsystemPathfinding _subsystemPathfinding = null!;

    private SubsystemTime _subsystemTime = null!;

    public Vector3? Destination { get; set; }

    public float Range { get; set; }

    public float Speed { get; set; }

    public int MaxPathfindingPositions { get; set; }

    public bool UseRandomMovements { get; set; }

    public bool IgnoreHeightDifference { get; set; }

    public bool RaycastDestination { get; set; }

    public ComponentBody? DoNotAvoidBody { get; set; }

    public bool IsStuck { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemTime.GameTime < _nextUpdateTime)
        {
            return;
        }

        var num = _random.Float(0.08f, 0.12f);
        _nextUpdateTime = _subsystemTime.GameTime + num;
        _pathfindingCongestion = MathUtils.Max(_pathfindingCongestion - 20f * num, 0f);
        _stateMachine.Update();
    }

    public void SetDestination(
        Vector3? destination,
        float speed,
        float range,
        int maxPathfindingPositions,
        bool useRandomMovements,
        bool ignoreHeightDifference,
        bool raycastDestination,
        ComponentBody? doNotAvoidBody
    )
    {
        Destination = destination;
        Speed = speed;
        Range = range;
        MaxPathfindingPositions = maxPathfindingPositions;
        UseRandomMovements = useRandomMovements;
        IgnoreHeightDifference = ignoreHeightDifference;
        RaycastDestination = raycastDestination;
        DoNotAvoidBody = doNotAvoidBody;
        _destinationChanged = true;
        _nextUpdateTime = 0.0;
    }

    public void Stop()
    {
        SetDestination(null, 0f, 0f, 0, false, false, false, null);
        _componentPilot.Stop();
        IsStuck = false;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemPathfinding = Project.FindSubsystem<SubsystemPathfinding>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPilot = Entity.FindComponent<ComponentPilot>(true)!;
        _stateMachine.AddState(
            "Stopped",
            delegate
            {
                Stop();
                _randomMoveCount = 0;
            },
            delegate
            {
                if (Destination.HasValue)
                {
                    _stateMachine.TransitionTo("MovingDirect");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "MovingDirect",
            delegate
            {
                IsStuck = false;
                _destinationChanged = true;
            },
            delegate
            {
                if (!Destination.HasValue)
                {
                    _stateMachine.TransitionTo("Stopped");
                }
                else if (_destinationChanged)
                {
                    _componentPilot.SetDestination(Destination, Speed, Range, IgnoreHeightDifference,
                        RaycastDestination,
                        Speed >= 1f, DoNotAvoidBody);
                    _destinationChanged = false;
                }
                else if (!_componentPilot.Destination.HasValue)
                {
                    _stateMachine.TransitionTo("Stopped");
                }
                else if (_componentPilot.IsStuck)
                {
                    if (MaxPathfindingPositions > 0 && _componentCreature.ComponentLocomotion.WalkSpeed > 0f)
                    {
                        _stateMachine.TransitionTo("SearchingForPath");
                    }
                    else if (UseRandomMovements)
                    {
                        _stateMachine.TransitionTo("MovingRandomly");
                    }
                    else
                    {
                        _stateMachine.TransitionTo("Stuck");
                    }
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "SearchingForPath",
            delegate
            {
                _pathfindingResult.IsCompleted = false;
                _pathfindingResult.IsInProgress = false;
            },
            delegate
            {
                if (!Destination.HasValue)
                {
                    _stateMachine.TransitionTo("Stopped");
                }
                else if (!_pathfindingResult.IsInProgress &&
                         (!_lastPathfindingTime.HasValue || _subsystemTime.GameTime - _lastPathfindingTime > 8.0) &&
                         _pathfindingCongestion < 500f)
                {
                    _lastPathfindingDestination = Destination.Value;
                    _lastPathfindingTime = _subsystemTime.GameTime;
                    var start = _componentCreature.ComponentBody.Position + new Vector3(0f, 0.01f, 0f);
                    var end = Destination.Value + new Vector3(0f, 0.01f, 0f);
                    var componentMiner = Entity.FindComponent<ComponentMiner>();
                    var ignoreDoors = componentMiner is { AutoInteractRate: > 0f } &&
                                      _random.Bool(0.5f);
                    _subsystemPathfinding.QueuePathSearch(start, end, 1f, _componentCreature.ComponentBody.BoxSize,
                        ignoreDoors, MaxPathfindingPositions, _pathfindingResult);
                }
                else if (UseRandomMovements)
                {
                    _stateMachine.TransitionTo("MovingRandomly");
                }

                if (!_pathfindingResult.IsCompleted)
                {
                    return;
                }

                _pathfindingCongestion =
                    MathUtils.Min(_pathfindingCongestion + _pathfindingResult.PositionsChecked, 1000f);
                if (_pathfindingResult.Path.Count > 0)
                {
                    _stateMachine.TransitionTo("MovingWithPath");
                }
                else if (UseRandomMovements)
                {
                    _stateMachine.TransitionTo("MovingRandomly");
                }
                else
                {
                    _stateMachine.TransitionTo("Stuck");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "MovingWithPath",
            delegate
            {
                _componentPilot.Stop();
                _randomMoveCount = 0;
            },
            delegate
            {
                if (!Destination.HasValue)
                {
                    _stateMachine.TransitionTo("Stopped");
                }
                else if (!_componentPilot.Destination.HasValue)
                {
                    if (_pathfindingResult.Path.Count > 0)
                    {
                        var value = _pathfindingResult.Path.Array[_pathfindingResult.Path.Count - 1];
                        _componentPilot.SetDestination(value, MathUtils.Min(Speed, 0.75f), 0.75f, false, false,
                            Speed >= 1f, DoNotAvoidBody);
                        _pathfindingResult.Path.RemoveAt(_pathfindingResult.Path.Count - 1);
                    }
                    else
                    {
                        _stateMachine.TransitionTo("MovingDirect");
                    }
                }
                else if (_componentPilot.IsStuck)
                {
                    _stateMachine.TransitionTo(UseRandomMovements ? "MovingRandomly" : "Stuck");
                }
                else
                {
                    var num = Vector3.DistanceSquared(_componentCreature.ComponentBody.Position, Destination.Value);
                    if (_lastPathfindingDestination is not null &&
                        Vector3.DistanceSquared(_lastPathfindingDestination.Value, Destination.Value) > num)
                    {
                        _stateMachine.TransitionTo("MovingDirect");
                    }
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "MovingRandomly",
            delegate
            {
                _componentPilot.SetDestination(
                    _componentCreature.ComponentBody.Position +
                    new Vector3(5f * _random.Float(-1f, 1f), 0f, 5f * _random.Float(-1f, 1f)), 1f, 1f, true, false,
                    false,
                    DoNotAvoidBody);
                _randomMoveCount++;
            },
            delegate
            {
                if (!Destination.HasValue)
                {
                    _stateMachine.TransitionTo("Stopped");
                }
                else if (_randomMoveCount > 3)
                {
                    _stateMachine.TransitionTo("Stuck");
                }
                else if (_componentPilot.IsStuck || !_componentPilot.Destination.HasValue)
                {
                    _stateMachine.TransitionTo("MovingDirect");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "Stuck",
            delegate { IsStuck = true; },
            delegate
            {
                if (!Destination.HasValue)
                {
                    _stateMachine.TransitionTo("Stopped");
                }
                else if (_destinationChanged)
                {
                    _destinationChanged = false;
                    _stateMachine.TransitionTo("MovingDirect");
                }
            },
            Actions.Empty
        );
        _stateMachine.TransitionTo("Stopped");
    }
}
