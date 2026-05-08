using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCetaceanBreatheBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _importanceLevel;

    private WhalePlumeParticleSystem? _particleSystem;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!IsActive)
        {
            stateMachine.TransitionTo("Inactive");
        }

        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                _importanceLevel = MathUtils.Lerp(0f, 400f,
                    MathUtils.Saturate((0.75f - _componentCreature.ComponentHealth.Air) / 0.75f));
                if (IsActive)
                {
                    stateMachine.TransitionTo("Surface");
                }
            },
            Actions.Empty
        );

        stateMachine.AddState(
            "Surface",
            delegate { _componentPathfinding.Stop(); },
            delegate
            {
                _ = _componentCreature.ComponentBody.Position;
                if (!_componentPathfinding.Destination.HasValue)
                {
                    var destination = FindSurfaceDestination();
                    if (destination.HasValue)
                    {
                        var speed = _componentCreature.ComponentHealth.Air < 0.25f ? 1f : _random.Float(0.4f, 0.6f);
                        _componentPathfinding.SetDestination(destination, speed, 1f, 0, false, false, false, null);
                    }
                }
                else if (_componentPathfinding.IsStuck)
                {
                    _importanceLevel = 0f;
                }

                if (_componentCreature.ComponentHealth.Air > 0.9f)
                {
                    stateMachine.TransitionTo("Breathe");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Breathe",
            delegate
            {
                var forward = _componentCreature.ComponentBody.Matrix.Forward;
                var value = _componentCreature.ComponentBody.Matrix.Translation + 10f * forward +
                            new Vector3(0f, 2f, 0f);
                _componentPathfinding.SetDestination(value, 0.6f, 1f, 0, false, false, false, null);
                _particleSystem = new WhalePlumeParticleSystem(_subsystemTerrain, _random.Float(0.8f, 1.1f),
                    _random.Float(1f, 1.3f));
                _subsystemParticles.AddParticleSystem(_particleSystem);
                _subsystemAudio.PlayRandomSound("Audio/Creatures/WhaleBlow", 1f, _random.Float(-0.2f, 0.2f),
                    _componentCreature.ComponentBody.Position, 10f, true);
            },
            delegate
            {
                _particleSystem?.Position = _componentCreature.ComponentBody.Position +
                                            new Vector3(0f, 0.8f * _componentCreature.ComponentBody.BoxSize.Y, 0f);
                if (!_subsystemParticles.ContainsParticleSystem(_particleSystem))
                {
                    _importanceLevel = 0f;
                }
            },
            delegate
            {
                _particleSystem?.IsStopped = true;
                _particleSystem = null;
            }
        );
    }

    public Vector3? FindSurfaceDestination()
    {
        var vector = 0.5f * (_componentCreature.ComponentBody.BoundingBox.Min +
                             _componentCreature.ComponentBody.BoundingBox.Max);
        var forward = _componentCreature.ComponentBody.Matrix.Forward;
        var s = 2f * _componentCreature.ComponentBody.ImmersionDepth;
        for (var i = 0; i < 16; i++)
        {
            var vector2 = i < 4
                ? new Vector2(forward.X, forward.Z) + _random.Vector2(0f, 0.25f)
                : _random.Vector2(0.5f, 1f);
            var v = Vector3.Normalize(new Vector3(vector2.X, 1f, vector2.Y));
            var end = vector + s * v;
            var terrainRaycastResult = _subsystemTerrain.Raycast(vector, end, false, false,
                (value, _) => Terrain.ExtractContents(value) != 18);
            if (terrainRaycastResult.HasValue && Terrain.ExtractContents(terrainRaycastResult.Value.Value) == 0)
            {
                return new Vector3(terrainRaycastResult.Value.CellFace.X + 0.5f, terrainRaycastResult.Value.CellFace.Y,
                    terrainRaycastResult.Value.CellFace.Z + 0.5f);
            }
        }

        return null;
    }
}
