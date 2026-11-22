using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components.Behavior;

public class ComponentSwimAroundBehavior : ComponentBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel = 1f;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (string.IsNullOrEmpty(stateMachine.CurrentState))
        {
            stateMachine.TransitionTo("Inactive");
        }

        if (_random.Float(0f, 1f) < 0.05f * dt)
        {
            _importanceLevel = _random.Float(1f, 3f);
        }

        if (IsActive)
        {
            stateMachine.Update();
        }
        else
        {
            stateMachine.TransitionTo("Inactive");
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Swim");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stuck",
            delegate
            {
                if (_random.Float(0f, 1f) < 0.5f)
                {
                    _importanceLevel = 1f;
                }

                stateMachine.TransitionTo("Swim");
            },
            Actions.Empty,
            Actions.Empty
        );
        stateMachine.AddState(
            "Swim",
            delegate { _componentPathfinding.Stop(); },
            delegate
            {
                _ = _componentCreature.ComponentBody.Position;
                if (!_componentPathfinding.Destination.HasValue)
                {
                    var destination = FindDestination();
                    if (destination.HasValue)
                    {
                        _componentPathfinding.SetDestination(
                            destination,
                            _random.Float(0.3f, 0.4f),
                            1f,
                            0,
                            false,
                            true,
                            false,
                            null
                        );
                    }
                    else
                    {
                        _importanceLevel = 1f;
                    }
                }
                else if (_componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("Stuck");
                }
            },
            Actions.Empty
        );
    }

    public Vector3? FindDestination()
    {
        var vector = 0.5f *
                     (_componentCreature.ComponentBody.BoundingBox.Min +
                      _componentCreature.ComponentBody.BoundingBox.Max);
        var bestDistance = 2f;
        Vector3? result = null;
        var probeDistance = _random.Float(10f, 16f);
        for (var i = 0; i < 16; i++)
        {
            var horizontalDirection = _random.Vector2(1f, 1f);
            var y = 0.3f * _random.Float(-0.9f, 1f);
            var v = Vector3.Normalize(new Vector3(horizontalDirection.X, y, horizontalDirection.Y));
            var probeEnd = vector + probeDistance * v;
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
            if (!terrainRaycastResult.HasValue)
            {
                if (!(probeDistance > bestDistance))
                {
                    continue;
                }

                result = probeEnd;
                bestDistance = probeDistance;
            }
            else if (terrainRaycastResult.Value.Distance > bestDistance)
            {
                result = vector + v * terrainRaycastResult.Value.Distance;
                bestDistance = terrainRaycastResult.Value.Distance;
            }
        }

        return result;
    }
}
