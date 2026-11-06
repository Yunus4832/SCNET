namespace Game.Modding;

/// <summary>A registered factory whose mutable instances belong to individual world runtimes.</summary>
public sealed class ModWorldState<T> where T : class, IDisposable
{
    private readonly Dictionary<WorldRuntimeContext, T> _instances = [];

    private readonly HashSet<WorldRuntimeContext> _creating = [];

    private readonly Func<WorldRuntimeContext, T> _factory;

    private readonly ModWorldStates _registry;

    private bool _removed;

    internal ModWorldState(ModWorldStates registry, Func<WorldRuntimeContext, T> factory)
    {
        _registry = registry;
        _factory = factory;
    }

    internal bool HasInstances => _instances.Count != 0 || _creating.Count != 0;

    public T Get(WorldRuntimeContext runtime)
    {
        lock (_registry.LifecycleGate)
        {
            if (_registry.IsRetiring)
            {
                throw new InvalidOperationException("The mod environment is being replaced.");
            }

            return GetOrCreate(runtime);
        }
    }

    private T GetOrCreate(WorldRuntimeContext runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ObjectDisposedException.ThrowIf(_removed, this);
        ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
        if (_instances.TryGetValue(runtime, out var existing))
        {
            return existing;
        }

        if (!_creating.Add(runtime))
        {
            throw new InvalidOperationException("A mod world state factory cannot recursively resolve itself.");
        }

        try
        {
            var state = _factory(runtime) ??
                        throw new InvalidOperationException("A mod world state factory returned null.");
            if (_instances.Values.Any(instance => ReferenceEquals(instance, state)))
            {
                throw new InvalidOperationException(
                    "A mod state factory must create a distinct instance for each world."
                );
            }

            try
            {
                runtime.OwnWorldResource(new StateLifetime(this, runtime, state));
            }
            catch
            {
                state.Dispose();
                throw;
            }

            _instances.Add(runtime, state);
            return state;
        }
        finally
        {
            _creating.Remove(runtime);
        }
    }

    internal void Remove()
    {
        if (HasInstances)
        {
            throw new InvalidOperationException("Release all world instances before removing a mod state factory.");
        }

        _removed = true;
    }

    private sealed class StateLifetime(
        ModWorldState<T> owner,
        WorldRuntimeContext runtime,
        T state
    ) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            lock (owner._registry.LifecycleGate)
            {
                if (_disposed) { return; }

                _disposed = true;
                owner._instances.Remove(runtime);
                state.Dispose();
            }
        }
    }
}
