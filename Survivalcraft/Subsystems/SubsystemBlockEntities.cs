using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

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
        if (GameManager.Role == WorldRole.RemoteClient)
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
        if (miner.ComponentCharacter != null)
        {
            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity")
                .SetValue("Owner", miner.ComponentCharacter.CharacterId);
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
            RemoveBlockEntity(componentBlockEntity);
        }
    }

    internal bool RemoveBlockEntity(ComponentBlockEntity block)
    {
        return BlockEntities.TryGetValue(block.Coordinates, out var registered) &&
               ReferenceEquals(registered, block) &&
               BlockEntities.Remove(block.Coordinates);
    }
}
