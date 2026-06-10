namespace Game.Modding;

public sealed class ExtensionRegistry
{
    private readonly Dictionary<RegistryKey, IRegistryControl> _registries = [];
    private bool _isFrozen;

    public NamespacedRegistry<T> GetRegistry<T>(string name) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var key = new RegistryKey(name, typeof(T));
        if (_registries.TryGetValue(key, out var existing))
        {
            return (NamespacedRegistry<T>)existing;
        }

        if (_isFrozen)
        {
            throw new InvalidOperationException("Extension registry is frozen.");
        }

        var registry = new NamespacedRegistry<T>();
        _registries.Add(key, registry);
        return registry;
    }

    internal void Freeze()
    {
        _isFrozen = true;
        foreach (var registry in _registries.Values)
        {
            registry.Freeze();
        }
    }

    internal void RemoveOwner(ModId owner)
    {
        foreach (var registry in _registries.Values)
        {
            registry.RemoveOwner(owner);
        }
    }

    private readonly record struct RegistryKey(string Name, Type ValueType);
}
