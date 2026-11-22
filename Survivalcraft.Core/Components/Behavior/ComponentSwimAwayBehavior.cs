using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components.Behavior;

public class ComponentSwimAwayBehavior : ComponentBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private ComponentFrame? _attacker;

    private ComponentCreature _componentCreature = null!;

    private ComponentHerdBehavior? _componentHerdBehavior;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _timeToForgetAttacker;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public void SwimAwayFrom(ComponentBody attacker)
    {
        _attacker = attacker;
        _timeToForgetAttacker = _random.Float(10f, 20f);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentHerdBehavior = Entity.FindComponent<ComponentHerdBehavior>();
        _componentCreature.ComponentHealth.Attacked += delegate(ComponentCreature attacker)
        {
            SwimAwayFrom(attacker.ComponentBody);
        };
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                _attacker = null;
            },
            delegate
            {
                if (_attacker != null)
                {
                    _timeToForgetAttacker -= _subsystemTime.GameTimeDelta;
                    if (_timeToForgetAttacker <= 0f)
                    {
                        _attacker = null;
                    }
                }

                if (_componentCreature.ComponentHealth.HealthChange < 0f)
                {
                    _importanceLevel = _componentCreature.ComponentHealth.Health < 0.33f ? 300 : 100;
                }
                else if (_attacker != null &&
                         Vector3.DistanceSquared(_attacker.Position, _componentCreature.ComponentBody.Position) < 25f)
                {
                    _importanceLevel = 100f;
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("SwimmingAway");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "SwimmingAway",
            delegate { _componentPathfinding.SetDestination(FindSafePlace(), 1f, 1f, 0, false, true, false, null); },
            delegate
            {
                if (!IsActive || !_componentPathfinding.Destination.HasValue || _componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("Inactive");
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public Vector3 FindSafePlace()
    {
        var vector = 0.5f *
                     (_componentCreature.ComponentBody.BoundingBox.Min +
                      _componentCreature.ComponentBody.BoundingBox.Max);
        var herdPosition = _componentHerdBehavior?.FindHerdCenter();
        var bestScore = float.NegativeInfinity;
        var result = vector;
        for (var i = 0; i < 40; i++)
        {
            var horizontalDirection = _random.Vector2(1f, 1f);
            var y = 0.4f * _random.Float(-1f, 1f);
            var v = Vector3.Normalize(new Vector3(horizontalDirection.X, y, horizontalDirection.Y));
            var probeEnd = vector + _random.Float(10f, 20f) * v;
            var terrainRaycastResult = _subsystemTerrain.Raycast(
                vector,
                probeEnd,
                false,
                false,
                delegate(int value, float d)
                {
                    var contents = Terrain.ExtractContents(value);
                    return !(WorldContentBinding.For(Project).Catalog.Blocks[contents] is WaterBlock);
                }
            );
            var candidatePosition = terrainRaycastResult.HasValue
                ? vector + v * terrainRaycastResult.Value.Distance
                : probeEnd;
            var candidateScore = ScoreSafePlace(vector, candidatePosition, herdPosition);
            if (!(candidateScore > bestScore))
            {
                continue;
            }

            bestScore = candidateScore;
            result = candidatePosition;
        }

        return result;
    }

    public float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? herdPosition)
    {
        var vector = new Vector2(currentPosition.X, currentPosition.Z);
        var candidateXZ = new Vector2(safePosition.X, safePosition.Z);
        var herdXZ = herdPosition.HasValue
            ? new Vector2?(new Vector2(herdPosition.Value.X, herdPosition.Value.Z))
            : null;
        var s = new Segment2(vector, candidateXZ);
        var pathDistanceFromHerd = herdXZ.HasValue ? Segment2.Distance(s, herdXZ.Value) : 0f;
        if (_attacker == null)
        {
            return 1.5f * Vector2.Distance(vector, candidateXZ) - pathDistanceFromHerd;
        }

        var position = _attacker.Position;
        var attackerXZ = new Vector2(position.X, position.Z);
        var distanceFromAttacker = Vector2.Distance(attackerXZ, candidateXZ);
        var pathDistanceFromAttacker = Segment2.Distance(s, attackerXZ);
        return distanceFromAttacker + 1.5f * pathDistanceFromAttacker - pathDistanceFromHerd;
    }
}
