using Game.Components;

namespace Game.Modding;

public interface IModPlayerContextActionHooks
{
    IDisposable ProvideNearbyAction(
        Func<PlayerContextActionQueryContext, PlayerContextAction?> provider,
        int priority = 0);
}

public sealed class PlayerContextActionHooks
{
    private readonly List<Registration> _registrations = [];
    private Registration[] _providers = [];
    private bool _isFrozen;
    private long _sequence;

    public PlayerContextAction? Resolve(PlayerContextActionQueryContext context)
    {
        foreach (var registration in _providers)
        {
            try
            {
                var action = registration.Provider(context);
                if (action is not null)
                {
                    return action;
                }
            }
            catch (Exception exception)
            {
                Log.Error($"Mod {registration.Owner} context action hook failed: {exception}");
            }
        }

        return null;
    }

    internal IModPlayerContextActionHooks ForOwner(ModId owner) => new OwnedPlayerContextActionHooks(owner, this);

    internal void Freeze()
    {
        _isFrozen = true;
        RebuildProviders();
    }

    internal void RemoveOwner(ModId owner)
    {
        _registrations.RemoveAll(registration => registration.Owner == owner);
        RebuildProviders();
    }

    private IDisposable Register(
        ModId owner,
        Func<PlayerContextActionQueryContext, PlayerContextAction?> provider,
        int priority)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (_isFrozen)
        {
            throw new InvalidOperationException("Player context action hooks are frozen.");
        }

        var registration = new Registration(this, owner, provider, priority, _sequence++);
        _registrations.Add(registration);
        return registration;
    }

    private void Remove(Registration registration)
    {
        _registrations.Remove(registration);
        RebuildProviders();
    }

    private void RebuildProviders()
    {
        _providers = _registrations
            .OrderByDescending(registration => registration.Priority)
            .ThenBy(registration => registration.Sequence)
            .ToArray();
    }

    private sealed class OwnedPlayerContextActionHooks(ModId owner, PlayerContextActionHooks hooks)
        : IModPlayerContextActionHooks
    {
        public IDisposable ProvideNearbyAction(
            Func<PlayerContextActionQueryContext, PlayerContextAction?> provider,
            int priority = 0) => hooks.Register(owner, provider, priority);
    }

    private sealed class Registration(
        PlayerContextActionHooks hooks,
        ModId owner,
        Func<PlayerContextActionQueryContext, PlayerContextAction?> provider,
        int priority,
        long sequence) : IDisposable
    {
        private PlayerContextActionHooks? _hooks = hooks;

        public ModId Owner { get; } = owner;

        public Func<PlayerContextActionQueryContext, PlayerContextAction?> Provider { get; } = provider;

        public int Priority { get; } = priority;

        public long Sequence { get; } = sequence;

        public void Dispose() => Interlocked.Exchange(ref _hooks, null)?.Remove(this);
    }
}

public sealed class PlayerContextActionQueryContext(ComponentPlayer componentPlayer, ComponentGui componentGui)
{
    public ComponentPlayer ComponentPlayer { get; } = componentPlayer;

    public ComponentGui ComponentGui { get; } = componentGui;
}

public sealed class PlayerContextActionExecutionContext(ComponentPlayer componentPlayer, ComponentGui componentGui)
{
    public ComponentPlayer ComponentPlayer { get; } = componentPlayer;

    public ComponentGui ComponentGui { get; } = componentGui;
}

public sealed class PlayerContextAction(string label, Action<PlayerContextActionExecutionContext> execute)
{
    public string Label { get; } = label;

    public Action<PlayerContextActionExecutionContext> Execute { get; } = execute;

    public bool IsChecked { get; init; }
}
