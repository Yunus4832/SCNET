namespace EntitySystem.Core;

public class EntityToIdMap(Dictionary<Entity, int> map)
{
    public int FindId(Entity? entity)
    {
        return entity is null ? 0 : map.GetValueOrDefault(entity, 0);
    }

    public int FindId(Component? component)
    {
        return component == null ? 0 : FindId(component.Entity);
    }
}
