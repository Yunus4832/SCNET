using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentShapeshifter : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public static readonly Random Random2 = new();

    private ComponentBody _componentBody = null!;

    private ComponentHealth _componentHealth = null!;

    private ComponentSpawn _componentSpawn = null!;

    private string _dayEntityTemplateName = string.Empty;

    private string _nightEntityTemplateName = string.Empty;

    private string _spawnEntityTemplateName = string.Empty;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemSky _subsystemSky = null!;

    private float _timeToSwitch;

    private double? _shapeshiftStartTime;

    public bool IsEnabled { get; set; }

    public double? ShapeshiftStartTime => _shapeshiftStartTime;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var runtime = WorldRuntimeContext.For(Project);
        if (!runtime.IsAuthority) { return; }

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
            else if (areSupernaturalCreaturesEnabled &&
                     _subsystemSky.SkyLightIntensity < 0.1f &&
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
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
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

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            _timeToSwitch = Random2.Float(3f, 15f);
            IsEnabled = Random2.Float(0f, 1f) < value;
        }

        _spawnEntityTemplateName = valuesDictionary.GetValue("SpawnEntityTemplateName", string.Empty);
        var startTime = valuesDictionary.GetValue("ShapeshiftStartTime", -1d);
        if (startTime >= 0) { ApplyShapeshift(startTime); }

        _componentSpawn.Despawned += ComponentSpawn_Despawned;
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("SpawnEntityTemplateName", _spawnEntityTemplateName);
        valuesDictionary.SetValue("ShapeshiftStartTime", _shapeshiftStartTime ?? -1d);
    }

    public override void OnEntityAdded()
    {
        if (_shapeshiftStartTime.HasValue)
        {
            _componentSpawn.DespawnDuration = 3f;
            _componentSpawn.DespawnTime = _shapeshiftStartTime.Value;
        }
    }

    public void ShapeshiftTo(string entityTemplateName)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || !string.IsNullOrEmpty(_spawnEntityTemplateName))
        {
            return;
        }

        _spawnEntityTemplateName = entityTemplateName;
        _componentSpawn.Despawn();
        ApplyShapeshift(_componentSpawn.DespawnTime!.Value);
        NetworkSender.SendToObservers(
            Entity,
            new CreatureShapeshiftStatePackage
            {
                EntityId = Entity.EntityId, StartTime = _shapeshiftStartTime!.Value
            }
        );
    }

    public void ApplyShapeshift(double startTime)
    {
        if (_shapeshiftStartTime.HasValue) { return; }

        _shapeshiftStartTime = startTime;
        _componentSpawn.DespawnDuration = 3f;
        _componentSpawn.DespawnTime = startTime;
    }

    public void ComponentSpawn_Despawned(ComponentSpawn componentSpawn)
    {
        if (_componentHealth.Health > 0f &&
            !string.IsNullOrEmpty(_spawnEntityTemplateName) &&
            WorldRuntimeContext.For(Project).IsAuthority)
        {
            var entity = DatabaseManager.CreateEntity(Project, _spawnEntityTemplateName, true)!;
            var componentBody = entity.FindComponent<ComponentBody>(true)!;
            componentBody.Position = _componentBody.Position;
            componentBody.Rotation = _componentBody.Rotation;
            componentBody.Velocity = _componentBody.Velocity;
            entity.FindComponent<ComponentSpawn>(true)!.SpawnDuration = 0.5f;
            Project.AddEntity(entity);
        }
    }
}
