namespace Game.Commands;

/// <summary>
///     Declarative contribution consumed by a command frontend adapter.
///     Bindings describe how a frontend creates commands; they never provide
///     principals or bypass command authorization.
/// </summary>
public interface ICommandAdapterBinding;

public interface ICommandFrontendAdapter<in TRequest>
{
    CommandResult Execute(TRequest request, CommandContext context);
}

public sealed record RegisteredCommandAdapter<TBinding>(
    ResourceId Id,
    TBinding Binding)
    where TBinding : class, ICommandAdapterBinding;

public sealed class CommandAdapterRegistry
{
    private readonly CommandRegistry _commands;

    private readonly Dictionary<Type, Dictionary<ResourceId, AdapterEntry>> _entries = [];

    private readonly Dictionary<string, AdapterEntry> _textLookup =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsFrozen { get; private set; }

    internal CommandAdapterRegistry(CommandRegistry commands)
    {
        _commands = commands;
    }

    internal IDisposable Register<TBinding>(
        ModId owner,
        ResourceId id,
        TBinding binding)
        where TBinding : class, ICommandAdapterBinding
    {
        ArgumentNullException.ThrowIfNull(binding);
        EnsureCanRegister(owner, id);
        var entries = GetEntries(typeof(TBinding), create: true);
        if (entries.ContainsKey(id))
        {
            throw new InvalidOperationException(
                $"Command adapter {typeof(TBinding).Name} {id} is already registered.");
        }

        var entry = new AdapterEntry(owner, id, binding);
        if (binding is TextCommand text)
        {
            ValidateAndIndexText(entry, text);
        }

        entries.Add(id, entry);
        return new Registration(this, owner, typeof(TBinding), id);
    }

    public IReadOnlyList<RegisteredCommandAdapter<TBinding>> Get<TBinding>()
        where TBinding : class, ICommandAdapterBinding
    {
        return GetEntries(typeof(TBinding), create: false)
            .Select(pair => new RegisteredCommandAdapter<TBinding>(
                pair.Key,
                (TBinding)pair.Value.Binding))
            .OrderBy(entry => entry.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGet<TBinding>(
        ResourceId id,
        out TBinding? binding)
        where TBinding : class, ICommandAdapterBinding
    {
        if (GetEntries(typeof(TBinding), create: false)
            .TryGetValue(id, out var entry))
        {
            binding = (TBinding)entry.Binding;
            return true;
        }

        binding = null;
        return false;
    }

    internal bool TryFindText(
        string name,
        out RegisteredCommandAdapter<TextCommand>? registered)
    {
        if (_textLookup.TryGetValue(name, out var entry))
        {
            registered = new RegisteredCommandAdapter<TextCommand>(
                entry.Id,
                (TextCommand)entry.Binding);
            return true;
        }

        registered = null;
        return false;
    }

    internal void Freeze()
    {
        ValidateBindings();
        IsFrozen = true;
    }

    internal void RemoveOwner(ModId owner)
    {
        foreach (var type in _entries.Keys.ToArray())
        {
            foreach (var id in _entries[type]
                         .Where(pair => pair.Value.Owner == owner)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                Remove(owner, type, id);
            }
        }
    }

    private void ValidateBindings()
    {
        foreach (var registered in Get<TextCommand>())
        {
            foreach (var route in registered.Binding.Routes)
            {
                if (!_commands.TryGetDefinition(route.CommandType, out _))
                {
                    throw new InvalidOperationException(
                        $"Text adapter {registered.Id} references unregistered command type " +
                        $"{route.CommandType.Name}.");
                }
            }
        }

        foreach (var registered in Get<HttpCommandBinding>())
        {
            if (!_commands.TryGetDefinition(registered.Id, out var command) ||
                command is null)
            {
                throw new InvalidOperationException(
                    $"HTTP adapter {registered.Id} has no command with the same identity.");
            }

            if (command.Definition.CommandType != registered.Binding.CommandType)
            {
                throw new InvalidOperationException(
                    $"HTTP adapter {registered.Id} creates {registered.Binding.CommandType.Name}, " +
                    $"but the identity belongs to {command.Definition.CommandType.Name}.");
            }
        }
    }

    private void ValidateAndIndexText(AdapterEntry entry, TextCommand command)
    {
        var names = new[] { command.Name }.Concat(command.Aliases).ToArray();
        var duplicate = names
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Text adapter {entry.Id} declares duplicate name \"{duplicate.Key}\".");
        }

        foreach (var name in names)
        {
            if (_textLookup.TryGetValue(name, out var existing))
            {
                throw new InvalidOperationException(
                    $"Command name or alias \"{name}\" conflicts with {existing.Id}.");
            }
        }

        foreach (var name in names)
        {
            _textLookup.Add(name, entry);
        }
    }

    private Dictionary<ResourceId, AdapterEntry> GetEntries(
        Type type,
        bool create)
    {
        if (_entries.TryGetValue(type, out var entries))
        {
            return entries;
        }

        if (!create)
        {
            return [];
        }

        entries = [];
        _entries.Add(type, entries);
        return entries;
    }

    private void EnsureCanRegister(ModId owner, ResourceId id)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("Command adapter registry is frozen.");
        }

        if (owner != id.Namespace)
        {
            throw new InvalidOperationException(
                $"Mod {owner} cannot register command adapter {id} in another namespace.");
        }
    }

    private void Remove(ModId owner, Type type, ResourceId id)
    {
        var entries = GetEntries(type, create: false);
        if (!entries.TryGetValue(id, out var entry) || entry.Owner != owner)
        {
            return;
        }

        entries.Remove(id);
        if (entry.Binding is TextCommand text)
        {
            foreach (var name in new[] { text.Name }.Concat(text.Aliases))
            {
                _textLookup.Remove(name);
            }
        }
    }

    private sealed record AdapterEntry(
        ModId Owner,
        ResourceId Id,
        ICommandAdapterBinding Binding);

    private sealed class Registration(
        CommandAdapterRegistry registry,
        ModId owner,
        Type type,
        ResourceId id) : IDisposable
    {
        private CommandAdapterRegistry? _registry = registry;

        public void Dispose()
        {
            Interlocked.Exchange(ref _registry, null)?.Remove(owner, type, id);
        }
    }
}
