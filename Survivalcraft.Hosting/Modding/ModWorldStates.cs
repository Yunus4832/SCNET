namespace Game.Modding;

public interface IModWorldStates
{
    ModWorldState<T> Register<T>(ResourceId id, Func<WorldRuntimeContext, T> factory) where T : class, IDisposable;
}

public sealed class ModWorldStates
{
    private readonly Dictionary<ResourceId, Registration> _registrations = [];

    internal object LifecycleGate { get; } = new();

    internal bool IsRetiring { get; set; }

    private bool _frozen;

    internal IModWorldStates ForOwner(ModId owner) => new OwnedWorldStates(owner, this);

    internal void Freeze() => _frozen = true;

    internal void EnsureWorldsReleased()
    {
        if (_registrations.Values.Any(registration => registration.HasInstances()))
        {
            throw new InvalidOperationException("Release all worlds before stopping their mod host.");
        }
    }

    internal void RemoveOwner(ModId owner)
    {
        foreach (var id in _registrations.Keys.Where(id => id.Namespace == owner).ToArray())
        {
            _registrations[id].Remove();
            _registrations.Remove(id);
        }
    }

    private ModWorldState<T> Register<T>(ModId owner, ResourceId id, Func<WorldRuntimeContext, T> factory)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_frozen) { throw new InvalidOperationException("Mod world state registrations are frozen."); }

        if (id.Namespace != owner) { throw new InvalidOperationException($"Mod {owner} cannot register {id}."); }

        var state = new ModWorldState<T>(this, factory);
        if (!_registrations.TryAdd(id, new Registration(() => state.HasInstances, state.Remove)))
        {
            throw new InvalidOperationException($"Mod world state {id} is already registered.");
        }

        return state;
    }

    private sealed record Registration(Func<bool> HasInstances, Action Remove);

    private sealed class OwnedWorldStates(ModId owner, ModWorldStates registry) : IModWorldStates
    {
        public ModWorldState<T> Register<T>(ResourceId id, Func<WorldRuntimeContext, T> factory)
            where T : class, IDisposable => registry.Register(owner, id, factory);
    }
}
