using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components.Behavior;

public class ComponentWalkAroundBehavior : ComponentBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        stateMachine.AddState(
            "Inactive",
            delegate { _importanceLevel = _random.Float(0f, 1f); },
            delegate
            {
                if (_random.Float(0f, 1f) < 0.05f * _subsystemTime.GameTimeDelta)
                {
                    _importanceLevel = _random.Float(1f, 2f);
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Walk");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Walk",
            delegate
            {
                var speed = _componentCreature.ComponentBody.ImmersionFactor > 0.5f ? 1f : _random.Float(0.25f, 0.35f);
                _componentPathfinding.SetDestination(FindDestination(), speed, 1f, 0, false, true, false, null);
            },
            delegate
            {
                if (_componentPathfinding.IsStuck || !IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }

                if (!_componentPathfinding.Destination.HasValue)
                {
                    if (_random.Float(0f, 1f) < 0.5f)
                    {
                        stateMachine.TransitionTo("Inactive");
                    }
                    else
                    {
                        stateMachine.TransitionTo(string.Empty);
                        stateMachine.TransitionTo("Walk");
                    }
                }

                if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                }

                _componentCreature.ComponentCreaturePose.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public Vector3 FindDestination()
    {
        var position = _componentCreature.ComponentBody.Position;
        var bestScore = 0f;
        var result = position;
        for (var i = 0; i < 16; i++)
        {
            var vector = Vector2.Normalize(_random.Vector2(1f)) * _random.Float(6f, 12f);
            var candidatePosition = new Vector3(position.X + vector.X, 0f, position.Z + vector.Y);
            candidatePosition.Y = _subsystemTerrain.Terrain.GetTopHeight(
                                      Terrain.ToCell(candidatePosition.X),
                                      Terrain.ToCell(candidatePosition.Z)
                                  ) +
                                  1;
            var candidateScore = ScoreDestination(candidatePosition);
            if (!(candidateScore > bestScore))
            {
                continue;
            }

            bestScore = candidateScore;
            result = candidatePosition;
        }

        return result;
    }

    public float ScoreDestination(Vector3 destination)
    {
        var score = 8f - MathUtils.Abs(_componentCreature.ComponentBody.Position.Y - destination.Y);
        if (_subsystemTerrain.Terrain.GetCellContents(
                Terrain.ToCell(destination.X),
                Terrain.ToCell(destination.Y) - 1,
                Terrain.ToCell(destination.Z)
            ) ==
            18)
        {
            score -= 5f;
        }

        return score;
    }
}
