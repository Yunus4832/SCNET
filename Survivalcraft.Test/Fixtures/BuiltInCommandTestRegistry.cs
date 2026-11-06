using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Modding;

namespace Survivalcraft.Test.Fixtures;

internal static class BuiltInCommandTestRegistry
{
    public static void RegisterWorld(CommandRegistry registry, ModId owner) =>
        BuiltInWorldCommands.Register(new DirectRegistration(registry, owner), owner);

    public static void Register(CommandRegistry registry, ModId owner)
    {
        var registration = new DirectRegistration(registry, owner);
        BuiltInWorldCommands.Register(registration, owner);
        BuiltInApplicationCommands.Register(registration, owner);
    }

    private sealed class DirectRegistration(
        CommandRegistry registry,
        ModId owner
    ) : IModCommands
    {
        public IModCommandAdapters Adapters { get; } =
            new DirectAdapterRegistration(registry.Adapters, owner);

        public IModCommandPermissions Permissions { get; } =
            new DirectPermissionRegistration(registry.Permissions, owner);

        public IDisposable Register<TCommand>(
            ResourceId id,
            CommandDefinition<TCommand> definition
        )
            where TCommand : IGameCommand
        {
            return registry.Register(owner, id, definition);
        }
    }

    private sealed class DirectPermissionRegistration(
        CommandPermissionRegistry permissions,
        ModId owner
    ) : IModCommandPermissions
    {
        public IDisposable Register(
            ResourceId id,
            CommandPermissionDefinition definition
        )
        {
            return permissions.Register(owner, id, definition);
        }
    }

    private sealed class DirectAdapterRegistration(
        CommandAdapterRegistry adapters,
        ModId owner
    ) : IModCommandAdapters
    {
        public IDisposable Register<TBinding>(
            ResourceId id,
            TBinding binding
        )
            where TBinding : class, ICommandAdapterBinding
        {
            return adapters.Register(owner, id, binding);
        }

        public IReadOnlyList<RegisteredCommandAdapter<TBinding>> Get<TBinding>()
            where TBinding : class, ICommandAdapterBinding
        {
            return adapters.Get<TBinding>();
        }

        public bool TryGet<TBinding>(ResourceId id, out TBinding? binding)
            where TBinding : class, ICommandAdapterBinding
        {
            return adapters.TryGet(id, out binding);
        }
    }
}
