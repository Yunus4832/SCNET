using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Components;

public class ComponentShapeshifter : Component, IUpdateable
{
    public readonly static Random Random2 = new();

    private ComponentBody _componentBody = null!;

    private ComponentHealth _componentHealth = null!;

    private ComponentSpawn _componentSpawn = null!;

    private string _dayEntityTemplateName = string.Empty;

    private string _nightEntityTemplateName = string.Empty;

    private ShapeshiftParticleSystem? _particleSystem;

    private string _spawnEntityTemplateName = string.Empty;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemSky _subsystemSky = null!;

    private float _timeToSwitch;

    public bool IsEnabled { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var areSupernaturalCreaturesEnabled = _subsystemGameInfo.WorldSettings.AreSupernaturalCreaturesEnabled;
        if (IsEnabled && !_componentSpawn.IsDespawning && _componentHealth.Health > 0f)
        {
            if (!areSupernaturalCreaturesEnabled && !string.IsNullOrEmpty(_dayEntityTemplateName))
            {
                ShapeshiftTo(_dayEntityTemplateName);
            }
            else if (_subsystemSky.SkyLightIntensity > 0.25f && !string.IsNullOrEmpty(_dayEntityTemplateName))
            {
                _timeToSwitch -= 2f * dt;
                if (_timeToSwitch <= 0f)
                {
                    ShapeshiftTo(_dayEntityTemplateName);
                }
            }
            else if (areSupernaturalCreaturesEnabled && _subsystemSky.SkyLightIntensity < 0.1f &&
                     (_subsystemSky.MoonPhase == 0 || _subsystemSky.MoonPhase == 4) &&
                     !string.IsNullOrEmpty(_nightEntityTemplateName))
            {
                _timeToSwitch -= dt;
                if (_timeToSwitch <= 0f)
                {
                    ShapeshiftTo(_nightEntityTemplateName);
                }
            }
        }

        if (!string.IsNullOrEmpty(_spawnEntityTemplateName))
        {
            if (_particleSystem == null)
            {
                _particleSystem = new ShapeshiftParticleSystem();
                _subsystemParticles.AddParticleSystem(_particleSystem);
            }

            _particleSystem.BoundingBox = _componentBody.BoundingBox;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _componentSpawn = Entity.FindComponent<ComponentSpawn>(true)!;
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentHealth = Entity.FindComponent<ComponentHealth>(true)!;
        _dayEntityTemplateName = valuesDictionary.GetValue<string>("DayEntityTemplateName");
        _nightEntityTemplateName = valuesDictionary.GetValue<string>("NightEntityTemplateName");
        var value = valuesDictionary.GetValue<float>("Probability");
        if (!string.IsNullOrEmpty(_dayEntityTemplateName))
        {
            DatabaseManager.FindEntityValuesDictionary(_dayEntityTemplateName, true);
        }

        if (!string.IsNullOrEmpty(_nightEntityTemplateName))
        {
            DatabaseManager.FindEntityValuesDictionary(_nightEntityTemplateName, true);
        }

        _timeToSwitch = Random2.Float(3f, 15f);
        IsEnabled = Random2.Float(0f, 1f) < value;
        _componentSpawn.Despawned += ComponentSpawn_Despawned;
    }

    public void ShapeshiftTo(string entityTemplateName)
    {
        if (!string.IsNullOrEmpty(_spawnEntityTemplateName))
        {
            return;
        }

        _spawnEntityTemplateName = entityTemplateName;
        _componentSpawn.DespawnDuration = 3f;
        _componentSpawn.Despawn();
        _subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, _componentBody.Position, 3f, true);
    }

    public void ComponentSpawn_Despawned(ComponentSpawn componentSpawn)
    {
        if (_componentHealth.Health > 0f && !string.IsNullOrEmpty(_spawnEntityTemplateName) &&
            CommonLib.WorkType != WorkType.Client)
        {
            var entity = DatabaseManager.CreateEntity(Project, _spawnEntityTemplateName, true)!;
            var componentBody = entity.FindComponent<ComponentBody>(true)!;
            componentBody.Position = _componentBody.Position;
            componentBody.Rotation = _componentBody.Rotation;
            componentBody.Velocity = _componentBody.Velocity;
            entity.FindComponent<ComponentSpawn>(true)!.SpawnDuration = 0.5f;
            Project.AddEntity(entity);
        }

        _particleSystem?.Stopped = true;
    }

    public override void OnEntityRemoved()
    {
        _particleSystem?.Stopped = true;
    }
}
