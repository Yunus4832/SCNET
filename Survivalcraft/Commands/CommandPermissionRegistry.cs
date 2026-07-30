using EntitySystem.Core;

using Game.Modding;

namespace Game.Commands;

public sealed class CommandPermissionRegistry
{
    private readonly Dictionary<ResourceId, Entry> _definitions = [];

    public bool IsFrozen { get; private set; }

    public IReadOnlyList<RegisteredCommandPermission> Definitions =>
        _definitions
            .Select(pair => new RegisteredCommandPermission(
                pair.Key,
                pair.Value.Definition))
            .OrderBy(entry => entry.Id.ToString(), StringComparer.Ordinal)
            .ToArray();

    internal IDisposable Register(
        ModId owner,
        ResourceId id,
        CommandPermissionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureCanRegister(owner, id);
        if (!_definitions.TryAdd(id, new Entry(owner, definition)))
        {
            throw new InvalidOperationException(
                $"Command permission {id} is already registered.");
        }

        return new Registration(this, owner, id);
    }

    public bool TryGet(
        ResourceId id,
        out RegisteredCommandPermission? registered)
    {
        if (_definitions.TryGetValue(id, out var entry))
        {
            registered = new RegisteredCommandPermission(id, entry.Definition);
            return true;
        }

        registered = null;
        return false;
    }

    public bool HasEffectivePermission(
        ResourceId id,
        CommandPrincipal principal,
        Project? project)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!_definitions.TryGetValue(id, out var entry))
        {
            return false;
        }

        if (principal.Is(CommandPrincipalKind.ServerOperator) ||
            principal.Is(CommandPrincipalKind.System))
        {
            return true;
        }

        if (entry.Definition.IsImplicitlyGranted(principal, project))
        {
            return true;
        }

        if (entry.Definition.GrantPolicy is PermissionGrantPolicy.OperatorOnly)
        {
            return false;
        }

        return principal.HasPermission(id);
    }

    public bool CanGrant(
        ResourceId id,
        CommandPrincipal principal,
        Project? project,
        bool canDelegate = false)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!_definitions.TryGetValue(id, out var entry) ||
            entry.Definition.GrantPolicy is PermissionGrantPolicy.OperatorOnly)
        {
            return false;
        }

        if (principal.Is(CommandPrincipalKind.ServerOperator))
        {
            return entry.Definition.GrantPolicy is PermissionGrantPolicy.Standard ||
                   !canDelegate;
        }

        if (entry.Definition.GrantPolicy is PermissionGrantPolicy.OperatorManaged)
        {
            return false;
        }

        if (principal.CanDelegate(id))
        {
            return true;
        }

        return _definitions.Any(pair =>
            pair.Value.Definition.ManagesStandardPermissions &&
            (canDelegate
                ? principal.CanDelegate(pair.Key)
                : HasEffectivePermission(pair.Key, principal, project)));
    }

    internal void Freeze()
    {
        IsFrozen = true;
    }

    internal void RemoveOwner(ModId owner)
    {
        foreach (var id in _definitions
                     .Where(pair => pair.Value.Owner == owner)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _definitions.Remove(id);
        }
    }

    private void EnsureCanRegister(ModId owner, ResourceId id)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException(
                "Command permission registry is frozen.");
        }

        if (owner != id.Namespace)
        {
            throw new InvalidOperationException(
                $"Mod {owner} cannot register permission {id} in another namespace.");
        }
    }

    private sealed record Entry(
        ModId Owner,
        CommandPermissionDefinition Definition);

    private sealed class Registration(
        CommandPermissionRegistry registry,
        ModId owner,
        ResourceId id) : IDisposable
    {
        private CommandPermissionRegistry? _registry = registry;

        public void Dispose()
        {
            var registry = Interlocked.Exchange(ref _registry, null);
            if (registry is not null &&
                registry._definitions.TryGetValue(id, out var entry) &&
                entry.Owner == owner)
            {
                registry._definitions.Remove(id);
            }
        }
    }
}
