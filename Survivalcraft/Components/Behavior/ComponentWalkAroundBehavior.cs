using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentWalkAroundBehavior : ComponentBehavior, IUpdateable
{
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

                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public Vector3 FindDestination()
    {
        var position = _componentCreature.ComponentBody.Position;
        var num = 0f;
        var result = position;
        for (var i = 0; i < 16; i++)
        {
            var vector = Vector2.Normalize(_random.Vector2(1f)) * _random.Float(6f, 12f);
            var vector2 = new Vector3(position.X + vector.X, 0f, position.Z + vector.Y);
            vector2.Y = _subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(vector2.X), Terrain.ToCell(vector2.Z)) +
                        1;
            var num2 = ScoreDestination(vector2);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            result = vector2;
        }

        return result;
    }

    public float ScoreDestination(Vector3 destination)
    {
        var num = 8f - MathUtils.Abs(_componentCreature.ComponentBody.Position.Y - destination.Y);
        if (_subsystemTerrain.Terrain.GetCellContents(Terrain.ToCell(destination.X), Terrain.ToCell(destination.Y) - 1,
                Terrain.ToCell(destination.Z)) == 18)
        {
            num -= 5f;
        }

        return num;
    }
}
