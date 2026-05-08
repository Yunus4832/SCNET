using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Subsystems;

public class SubsystemBlockEntities : Subsystem
{
    public readonly Dictionary<Point3, ComponentBlockEntity> BlockEntities = new();

    public ComponentBlockEntity? GetBlockEntity(int x, int y, int z)
    {
        BlockEntities.TryGetValue(new Point3(x, y, z), out var value);
        return value;
    }

    public void CreateBlockEntity(string entityName, Point3 point, ComponentMiner miner)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        CreateBlockEntityLogic(entityName, point, miner);
    }

    private void CreateBlockEntityLogic(string entityName, Point3 point, ComponentMiner miner)
    {
        var databaseObject = Project.GameDatabase.Database.FindDatabaseObject(
            entityName,
            Project.GameDatabase.EntityTemplateType,
            true
        )!;
        var valuesDictionary = new ValuesDictionary();
        valuesDictionary.PopulateFromDatabaseObject(databaseObject);
        valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point);
        if (miner.ComponentPlayer != null)
        {
            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity")
                .SetValue("Owner", miner.ComponentPlayer.PlayerGuid);
        }

        var entity = Project.CreateEntity(valuesDictionary);
        Project.AddEntity(entity);
    }

    public override void OnEntityAdded(Entity entity)
    {
        var componentBlockEntity = entity.FindComponent<ComponentBlockEntity>();
        if (componentBlockEntity == null)
        {
            return;
        }

        if (BlockEntities.TryAdd(componentBlockEntity.Coordinates, componentBlockEntity))
        {
            return;
        }

        componentBlockEntity.Project.RemoveEntity(componentBlockEntity.Entity, true);
    }

    public override void OnEntityRemoved(Entity entity)
    {
        var componentBlockEntity = entity.FindComponent<ComponentBlockEntity>();
        if (componentBlockEntity != null)
        {
            BlockEntities.Remove(componentBlockEntity.Coordinates);
        }
    }
}
