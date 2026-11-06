namespace Game.Modding;

internal interface IRegistryControl
{
    void Freeze();

    void RemoveOwner(ModId owner);
}

public sealed class NamespacedRegistry<T> : IRegistryControl where T : class
{
    private readonly Dictionary<ResourceId, Entry> _entries = [];

    public bool IsFrozen { get; private set; }

    public IReadOnlyCollection<ResourceId> Keys => _entries.Keys;

    public IReadOnlyList<KeyValuePair<ResourceId, T>> Entries => _entries
        .Select(pair => new KeyValuePair<ResourceId, T>(pair.Key, pair.Value.Value))
        .ToArray();

    internal IDisposable Register(ModId owner, ResourceId id, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsFrozen)
        {
            throw new InvalidOperationException("Registry is frozen.");
        }

        if (owner != id.Namespace)
        {
            throw new InvalidOperationException($"Mod {owner} cannot register resource {id} in another namespace.");
        }

        return !_entries.TryAdd(id, new Entry(owner, value))
            ? throw new InvalidOperationException($"Resource {id} is already registered.")
            : new Registration(this, id, owner);
    }

    public bool TryGet(ResourceId id, out T? value)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    void IRegistryControl.Freeze() => IsFrozen = true;

    void IRegistryControl.RemoveOwner(ModId owner)
    {
        foreach (var id in _entries.Where(pair => pair.Value.Owner == owner).Select(pair => pair.Key).ToArray())
        {
            _entries.Remove(id);
        }
    }

    private void Remove(ResourceId id, ModId owner)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Owner == owner)
        {
            _entries.Remove(id);
        }
    }

    private sealed record Entry(ModId Owner, T Value);

    private sealed class Registration(NamespacedRegistry<T> registry, ResourceId id, ModId owner) : IDisposable
    {
        private NamespacedRegistry<T>? _registry = registry;

        public void Dispose()
        {
            Interlocked.Exchange(ref _registry, null)?.Remove(id, owner);
        }
    }
}
