namespace Game.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<ResourceId, DefinitionEntry> _definitions = [];
    private readonly Dictionary<Type, DefinitionEntry> _definitionsByType = [];

    public bool IsFrozen { get; private set; }

    public CommandAdapterRegistry Adapters { get; }

    public CommandPermissionRegistry Permissions { get; } = new();

    public IReadOnlyList<RegisteredGameCommand> Definitions => _definitions
        .Select(pair => new RegisteredGameCommand(pair.Key, pair.Value.Definition))
        .OrderBy(entry => entry.Id.ToString(), StringComparer.Ordinal)
        .ToArray();

    public CommandRegistry()
    {
        Adapters = new CommandAdapterRegistry(this);
    }

    internal IDisposable Register<TCommand>(
        ModId owner,
        ResourceId id,
        CommandDefinition<TCommand> definition)
        where TCommand : IGameCommand
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureCanRegister(owner, id);
        if (_definitions.ContainsKey(id))
        {
            throw new InvalidOperationException($"Command resource {id} is already registered.");
        }

        if (_definitionsByType.TryGetValue(typeof(TCommand), out var existing))
        {
            throw new InvalidOperationException(
                $"Command type {typeof(TCommand).Name} is already registered as {existing.Id}.");
        }

        var entry = new DefinitionEntry(owner, id, definition);
        _definitions.Add(id, entry);
        _definitionsByType.Add(typeof(TCommand), entry);
        return new Registration(this, owner, id);
    }

    public bool TryGetDefinition(Type commandType, out RegisteredGameCommand? registered)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        if (_definitionsByType.TryGetValue(commandType, out var entry))
        {
            registered = new RegisteredGameCommand(entry.Id, entry.Definition);
            return true;
        }

        registered = null;
        return false;
    }

    public bool TryGetDefinition(
        ResourceId commandId,
        out RegisteredGameCommand? registered)
    {
        if (_definitions.TryGetValue(commandId, out var entry))
        {
            registered = new RegisteredGameCommand(entry.Id, entry.Definition);
            return true;
        }

        registered = null;
        return false;
    }

    public bool TryGetDefinition<TCommand>(out RegisteredGameCommand? registered)
        where TCommand : IGameCommand
    {
        return TryGetDefinition(typeof(TCommand), out registered);
    }

    public bool TryEncode(
        IGameCommand command,
        out ResourceId commandId,
        out byte[] payload,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_definitionsByType.TryGetValue(command.GetType(), out var entry))
        {
            commandId = default;
            payload = [];
            error = CommandText.Get(
                "CommandTypeUnregistered_Message",
                "未注册的命令类型：{0}。",
                command.GetType().Name);
            return false;
        }

        if (!entry.Definition.SupportsRemoteInvocation)
        {
            commandId = default;
            payload = [];
            error = CommandText.Get(
                "CommandRemoteUnsupported_Message",
                "命令 {0} 不支持远程调用。",
                entry.Id.ToString());
            return false;
        }

        commandId = entry.Id;
        payload = entry.Definition.Encode(command);
        error = string.Empty;
        return true;
    }

    public bool TryDecode(
        ResourceId commandId,
        byte[] payload,
        out IGameCommand? command,
        out string error)
    {
        if (!_definitions.TryGetValue(commandId, out var entry))
        {
            command = null;
            error = CommandText.Get(
                "CommandIdentityUnknown_Message",
                "未知命令：{0}。",
                commandId.ToString());
            return false;
        }

        if (!entry.Definition.SupportsRemoteInvocation)
        {
            command = null;
            error = CommandText.Get(
                "CommandRemoteUnsupported_Message",
                "命令 {0} 不支持远程调用。",
                commandId.ToString());
            return false;
        }

        try
        {
            command = entry.Definition.Decode(payload);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to decode command {commandId}: {exception}");
            command = null;
            error = CommandText.Get(
                "CommandDataInvalid_Message",
                "命令 {0} 的数据无效。",
                commandId.ToString());
            return false;
        }
    }

    public bool CanInvoke(
        Type commandType,
        CommandPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(principal);
        return _definitionsByType.TryGetValue(commandType, out var entry) &&
               entry.Definition.IsPotentiallyAuthorized(
                   Permissions,
                   principal,
                   principal.Player?.Project ?? GameManager.Project);
    }

    public bool CanInvoke(
        IGameCommand command,
        CommandPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CanInvoke(command.GetType(), principal);
    }

    internal void Freeze()
    {
        ValidatePermissions();
        Permissions.Freeze();
        Adapters.Freeze();
        IsFrozen = true;
    }

    internal void RemoveOwner(ModId owner)
    {
        Adapters.RemoveOwner(owner);
        Permissions.RemoveOwner(owner);

        foreach (var id in _definitions
                     .Where(pair => pair.Value.Owner == owner)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            RemoveDefinition(owner, id);
        }
    }

    private void ValidatePermissions()
    {
        foreach (var command in Definitions)
        {
            if (command.Definition.RequiredPermission is not { } permission)
            {
                continue;
            }

            if (!Permissions.TryGet(permission, out var registered) ||
                registered is null)
            {
                throw new InvalidOperationException(
                    $"Command {command.Id} references unregistered permission {permission}.");
            }

            if (registered.Definition.Domain != command.Definition.Domain)
            {
                throw new InvalidOperationException(
                    $"Command {command.Id} belongs to {command.Definition.Domain}, " +
                    $"but permission {permission} belongs to {registered.Definition.Domain}.");
            }
        }
    }

    private void EnsureCanRegister(ModId owner, ResourceId id)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("Command registry is frozen.");
        }

        if (owner != id.Namespace)
        {
            throw new InvalidOperationException($"Mod {owner} cannot register command {id} in another namespace.");
        }
    }

    private void RemoveDefinition(ModId owner, ResourceId id)
    {
        if (!_definitions.TryGetValue(id, out var entry) || entry.Owner != owner)
        {
            return;
        }

        _definitions.Remove(id);
        _definitionsByType.Remove(entry.Definition.CommandType);
    }

    private sealed record DefinitionEntry(
        ModId Owner,
        ResourceId Id,
        ICommandDefinition Definition);

    private sealed class Registration(
        CommandRegistry registry,
        ModId owner,
        ResourceId id) : IDisposable
    {
        private CommandRegistry? _registry = registry;

        public void Dispose()
        {
            var registry = Interlocked.Exchange(ref _registry, null);
            registry?.RemoveDefinition(owner, id);
        }
    }
}
