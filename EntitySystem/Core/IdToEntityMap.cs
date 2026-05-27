namespace EntitySystem.Core;

public class IdToEntityMap(Dictionary<int, Entity> map)
{
    public static readonly IdToEntityMap Default = new IdToEntityMap(new Dictionary<int, Entity>());

    public Entity? FindEntity(int id)
    {
        return map.GetValueOrDefault(id);
    }

    public T? FindComponent<T>(int id, string name) where T : Component
    {
        var entity = FindEntity(id);
        return entity?.FindComponent<T>(name, false);
    }
}
