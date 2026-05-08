using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Components;

public class ComponentSpawn : Component, IUpdateable
{
    public ComponentBody? Body;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public ComponentFrame ComponentFrame { get; set; } = null!;

    public ComponentCreature? ComponentCreature { get; set; }

    public bool AutoDespawn { get; set; }

    public bool IsDespawning => DespawnTime.HasValue;

    public double SpawnTime { get; set; }

    public double? DespawnTime { get; set; }

    public float SpawnDuration { get; set; }

    public float DespawnDuration { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!DespawnTime.HasValue ||
            !(_subsystemGameInfo.TotalElapsedGameTime >= DespawnTime.Value + DespawnDuration))
        {
            return;
        }

        Project.RemoveEntity(Entity, true);
        Despawned?.Invoke(this);
    }

    public event Action<ComponentSpawn>? Despawned;

    public void Despawn()
    {
        DespawnTime ??= _subsystemGameInfo.TotalElapsedGameTime;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        Body = Entity.FindComponent<ComponentBody>();
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        ComponentFrame = Entity.FindComponent<ComponentFrame>(true)!;
        ComponentCreature = Entity.FindComponent<ComponentCreature>();
        AutoDespawn = valuesDictionary.GetValue<bool>("AutoDespawn");
        var value = valuesDictionary.GetValue<double>("SpawnTime");
        var value2 = valuesDictionary.GetValue<double>("DespawnTime");
        //解决客户端动物隐形问题
        SpawnDuration = CommonLib.WorkType == WorkType.Client ? 0f : 2f;
        DespawnDuration = 2f;
        SpawnTime = value < 0.0 ? _subsystemGameInfo.TotalElapsedGameTime : value;
        DespawnTime = value2 >= 0.0 ? new double?(value2) : null;
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("SpawnTime", SpawnTime);
        if (DespawnTime.HasValue && ComponentCreature is { ComponentHealth.Health: <= 0.0f })
        {
            valuesDictionary.SetValue("DespawnTime", DespawnTime.Value);
        }
    }
}
