using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentSwimAwayBehavior : ComponentBehavior, IUpdateable
{
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
        var vector = 0.5f * (_componentCreature.ComponentBody.BoundingBox.Min +
                             _componentCreature.ComponentBody.BoundingBox.Max);
        var herdPosition = _componentHerdBehavior?.FindHerdCenter();
        var num = float.NegativeInfinity;
        var result = vector;
        for (var i = 0; i < 40; i++)
        {
            var vector2 = _random.Vector2(1f, 1f);
            var y = 0.4f * _random.Float(-1f, 1f);
            var v = Vector3.Normalize(new Vector3(vector2.X, y, vector2.Y));
            var vector3 = vector + _random.Float(10f, 20f) * v;
            var terrainRaycastResult = _subsystemTerrain.Raycast(vector, vector3, false, false,
                delegate(int value, float d)
                {
                    var num3 = Terrain.ExtractContents(value);
                    return !(BlocksManager.Blocks[num3] is WaterBlock);
                });
            var vector4 = terrainRaycastResult.HasValue ? vector + v * terrainRaycastResult.Value.Distance : vector3;
            var num2 = ScoreSafePlace(vector, vector4, herdPosition);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            result = vector4;
        }

        return result;
    }

    public float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? herdPosition)
    {
        var vector = new Vector2(currentPosition.X, currentPosition.Z);
        var vector2 = new Vector2(safePosition.X, safePosition.Z);
        var vector3 = herdPosition.HasValue
            ? new Vector2?(new Vector2(herdPosition.Value.X, herdPosition.Value.Z))
            : null;
        var s = new Segment2(vector, vector2);
        var num = vector3.HasValue ? Segment2.Distance(s, vector3.Value) : 0f;
        if (_attacker == null)
        {
            return 1.5f * Vector2.Distance(vector, vector2) - num;
        }

        var position = _attacker.Position;
        var vector4 = new Vector2(position.X, position.Z);
        var num2 = Vector2.Distance(vector4, vector2);
        var num3 = Segment2.Distance(s, vector4);
        return num2 + 1.5f * num3 - num;

    }
}
