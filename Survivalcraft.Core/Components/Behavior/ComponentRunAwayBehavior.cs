using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components.Behavior;

public class ComponentRunAwayBehavior : ComponentBehavior, IUpdateable, INoiseListener
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private ComponentFrame? _attacker;

    private ComponentCreature _componentCreature = null!;

    private ComponentHerdBehavior? _componentHerdBehavior;

    private ComponentPathfinding _componentPathfinding = null!;

    private bool _heardNoise;

    private float _importanceLevel;

    private Vector3? _lastNoiseSourcePosition;

    private readonly Random _random = new();

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _timeToForgetAttacker;

    public override float ImportanceLevel => _importanceLevel;

    public void HearNoise(ComponentBody? sourceBody, Vector3 sourcePosition, float loudness)
    {
        if (!(loudness >= 1f))
        {
            return;
        }

        _heardNoise = true;
        _lastNoiseSourcePosition = sourcePosition;
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
        _heardNoise = false;
    }

    public void RunAwayFrom(ComponentBody componentBody)
    {
        _attacker = componentBody;
        _timeToForgetAttacker = _random.Float(10f, 20f);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentHerdBehavior = Entity.FindComponent<ComponentHerdBehavior>();
        _componentCreature.ComponentHealth.Attacked += delegate(ComponentCreature attacker)
        {
            RunAwayFrom(attacker.ComponentBody);
        };
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                _lastNoiseSourcePosition = null;
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

                if (_componentCreature.ComponentHealth.HealthChange < 0f ||
                    (_attacker != null &&
                     Vector3.DistanceSquared(
                         _attacker.Position,
                         _componentCreature.ComponentBody
                             .Position
                     ) <
                     36f))
                {
                    _importanceLevel = MathUtils.Max(
                        _importanceLevel,
                        _componentCreature.ComponentHealth.Health < 0.33f ? 300 : 100
                    );
                }
                else if (_heardNoise)
                {
                    _importanceLevel = MathUtils.Max(_importanceLevel, 5f);
                }
                else if (!IsActive)
                {
                    _importanceLevel = 0f;
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("RunningAway");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "RunningAway",
            delegate
            {
                var value = FindSafePlace();
                _componentPathfinding.SetDestination(value, 1f, 1f, 0, false, true, false, null);
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
                _subsystemNoise.MakeNoise(_componentCreature.ComponentBody, 0.25f, 6f);
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (!_componentPathfinding.Destination.HasValue || _componentPathfinding.IsStuck)
                {
                    _importanceLevel = 0f;
                }
                else if (_attacker != null)
                {
                    if (!_attacker.IsAddedToProject)
                    {
                        _importanceLevel = 0f;
                        _attacker = null;
                    }
                    else
                    {
                        var componentHealth = _attacker.Entity.FindComponent<ComponentHealth>();
                        if (componentHealth != null && componentHealth.Health == 0f)
                        {
                            _importanceLevel = 0f;
                            _attacker = null;
                        }
                    }
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public Vector3 FindSafePlace()
    {
        var position = _componentCreature.ComponentBody.Position;
        var herdPosition = _componentHerdBehavior?.FindHerdCenter();
        if (herdPosition.HasValue && Vector3.DistanceSquared(position, herdPosition.Value) < 144f)
        {
            herdPosition = null;
        }

        var bestScore = float.NegativeInfinity;
        var result = position;
        for (var i = 0; i < 30; i++)
        {
            var sampleX = Terrain.ToCell(position.X + _random.Float(-25f, 25f));
            var sampleZ = Terrain.ToCell(position.Z + _random.Float(-25f, 25f));
            for (var sampleY = 255; sampleY >= 0; sampleY--)
            {
                var cellContents = _subsystemTerrain.Terrain.GetCellContents(sampleX, sampleY, sampleZ);
                if (WorldContentBinding.For(Project).Catalog.Blocks[cellContents].Collidable ||
                    cellContents == 18)
                {
                    var vector = new Vector3(sampleX + 0.5f, sampleY + 1.1f, sampleZ + 0.5f);
                    var candidateScore = ScoreSafePlace(
                        position,
                        vector,
                        herdPosition,
                        _lastNoiseSourcePosition,
                        cellContents
                    );
                    if (candidateScore > bestScore)
                    {
                        bestScore = candidateScore;
                        result = vector;
                    }

                    break;
                }
            }
        }

        return result;
    }

    public float ScoreSafePlace(
        Vector3 currentPosition,
        Vector3 safePosition,
        Vector3? herdPosition,
        Vector3? noiseSourcePosition,
        int contents
    )
    {
        var score = 0f;
        var vector = new Vector2(currentPosition.X, currentPosition.Z);
        var candidateXZ = new Vector2(safePosition.X, safePosition.Z);
        var s = new Segment2(vector, candidateXZ);
        if (_attacker != null)
        {
            var position = _attacker.Position;
            var attackerXZ = new Vector2(position.X, position.Z);
            var distanceFromAttacker = Vector2.Distance(attackerXZ, candidateXZ);
            var pathDistanceFromAttacker = Segment2.Distance(s, attackerXZ);
            score += distanceFromAttacker + 3f * pathDistanceFromAttacker;
        }
        else
        {
            score += 2f * Vector2.Distance(vector, candidateXZ);
        }

        var herdXZ = herdPosition.HasValue
            ? new Vector2?(new Vector2(herdPosition.Value.X, herdPosition.Value.Z))
            : null;
        var pathDistanceFromHerd = herdXZ.HasValue ? Segment2.Distance(s, herdXZ.Value) : 0f;
        score -= pathDistanceFromHerd;
        var noiseXZ = noiseSourcePosition.HasValue
            ? new Vector2?(new Vector2(noiseSourcePosition.Value.X, noiseSourcePosition.Value.Z))
            : null;
        var pathDistanceFromNoise = noiseXZ.HasValue ? Segment2.Distance(s, noiseXZ.Value) : 0f;
        score += 1.5f * pathDistanceFromNoise;
        if (contents == 18)
        {
            score -= 4f;
        }

        return score;
    }
}
