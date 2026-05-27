using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFlyAwayBehavior : ComponentBehavior, IUpdateable, INoiseListener
{
    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private double _nextUpdateTime;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public override bool IsActive
    {
        set
        {
            base.IsActive = value;
            if (IsActive)
            {
                _nextUpdateTime = 0.0;
            }
        }
    }

    public void HearNoise(ComponentBody? sourceBody, Vector3 sourcePosition, float loudness)
    {
        if (loudness >= 0.25f && stateMachine.CurrentState != "RunningAway")
        {
            stateMachine.TransitionTo("DangerDetected");
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_componentCreature.ComponentHealth.HealthChange < 0f)
        {
            stateMachine.TransitionTo("DangerDetected");
        }

        if (!(_subsystemTime.GameTime >= _nextUpdateTime))
        {
            return;
        }

        _nextUpdateTime = _subsystemTime.GameTime + _random.Float(0.5f, 1f);
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentCreature.ComponentBody.CollidedWithBody += delegate
        {
            if (stateMachine.CurrentState != "RunningAway")
            {
                stateMachine.TransitionTo("DangerDetected");
            }
        };
        stateMachine.AddState(
            "LookingForDanger",
            Actions.Empty,
            delegate
            {
                if (ScanForDanger())
                {
                    stateMachine.TransitionTo("DangerDetected");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "DangerDetected",
            delegate
            {
                _importanceLevel = _componentCreature.ComponentHealth.Health < 0.33f ? 300 : 100;
                _nextUpdateTime = 0.0;
            },
            delegate
            {
                if (!IsActive)
                {
                    return;
                }

                stateMachine.TransitionTo("RunningAway");
                _nextUpdateTime = 0.0;
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "RunningAway",
            delegate
            {
                _componentPathfinding.SetDestination(FindSafePlace(), 1f, 1f, 0, false, true, false, null);
                _subsystemAudio.PlayRandomSound("Audio/Creatures/Wings", 0.8f, _random.Float(-0.1f, 0.2f),
                    _componentCreature.ComponentBody.Position, 3f, true);
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
                _subsystemNoise.MakeNoise(_componentCreature.ComponentBody, 0.25f, 6f);
            },
            delegate
            {
                if (!IsActive || !_componentPathfinding.Destination.HasValue || _componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("LookingForDanger");
                }
                else if (ScoreSafePlace(_componentCreature.ComponentBody.Position,
                             _componentPathfinding.Destination.Value, null) < 4f)
                {
                    _componentPathfinding.SetDestination(FindSafePlace(), 1f, 0.5f, 0, false, true, false, null);
                }
            },
            delegate { _importanceLevel = 0f; }
        );
        stateMachine.TransitionTo("LookingForDanger");
    }

    public bool ScanForDanger()
    {
        var matrix = _componentCreature.ComponentBody.Matrix;
        var translation = matrix.Translation;
        var forward = matrix.Forward;
        return ScoreSafePlace(translation, translation, forward) < 7f;
    }

    public Vector3 FindSafePlace()
    {
        var position = _componentCreature.ComponentBody.Position;
        var num = float.NegativeInfinity;
        var result = position;
        for (var i = 0; i < 20; i++)
        {
            var num2 = Terrain.ToCell(position.X + _random.Float(-20f, 20f));
            var num3 = Terrain.ToCell(position.Z + _random.Float(-20f, 20f));
            for (var num4 = 255; num4 >= 0; num4--)
            {
                var cellContents = _subsystemTerrain.Terrain.GetCellContents(num2, num4, num3);
                if (!BlocksManager.Blocks[cellContents].Collidable && cellContents != 18)
                {
                    continue;
                }

                var vector = new Vector3(num2 + 0.5f, num4 + 1.1f, num3 + 0.5f);
                var num5 = ScoreSafePlace(position, vector, null);
                if (num5 > num)
                {
                    num = num5;
                    result = vector;
                }

                break;
            }
        }

        return result;
    }

    public float ScoreSafePlace(Vector3 currentPosition, Vector3 safePosition, Vector3? lookDirection)
    {
        var num = 16f;
        var position = _componentCreature.ComponentBody.Position;
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), 16f, _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentBody = _componentBodies.Array[i];
            if (!IsPredator(componentBody.Entity))
            {
                continue;
            }

            var position2 = componentBody.Position;
            var v = safePosition - position2;
            if (lookDirection.HasValue && !(0f - Vector3.Dot(lookDirection.Value, v) > 0f))
            {
                continue;
            }

            if (v.Y >= 4f)
            {
                v *= 2f;
            }

            num = MathUtils.Min(num, v.Length());
        }

        var num2 = Vector3.Distance(currentPosition, safePosition);
        if (num2 < 8f)
        {
            return num * 0.5f;
        }

        return num * MathUtils.Lerp(1f, 0.75f, MathUtils.Saturate(num2 / 20f));
    }

    public bool IsPredator(Entity entity)
    {
        if (entity == Entity)
        {
            return false;
        }

        var componentCreature = entity.FindComponent<ComponentCreature>();
        return componentCreature is
        {
            Category: CreatureCategory.LandPredator
            or CreatureCategory.WaterPredator
            or CreatureCategory.LandOther
        };
    }
}
