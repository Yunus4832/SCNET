using EntitySystem.Core;

namespace Game.Subsystems;

public class SubsystemNames : Subsystem
{
    private readonly Dictionary<string, ComponentName> _componentsByName = new();

    public Component? FindComponentByName(string name, Type componentType, string componentName)
    {
        return FindEntityByName(name)?.FindComponent(componentType, componentName, false);
    }

    public T? FindComponentByName<T>(string name, string componentName) where T : Component
    {
        var entity = FindEntityByName(name);
        return entity?.FindComponent<T>(componentName, false);
    }

    public Entity? FindEntityByName(string name)
    {
        _componentsByName.TryGetValue(name, out var value);
        return value?.Entity;
    }

    public static string GetEntityName(Entity entity)
    {
        var componentName = entity.FindComponent<ComponentName>();
        return componentName != null ? componentName.Name : string.Empty;
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentName>())
        {
            if (item != null)
            {
                _componentsByName.Add(item.Name, item);
            }
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentName>())
        {
            if (item != null)
            {
                _componentsByName.Remove(item.Name);
            }
        }
    }
}
