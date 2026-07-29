using Game.Commands;

namespace Game.Modding;

public interface IMod
{
    void Configure(IModContext context);

    void Start(IModContext context);

    void Stop();
}

public interface IModContext
{
    ModManifest Manifest { get; }

    IModExtensions Extensions { get; }

    IModGameplayHooks Gameplay { get; }

    IModBlockBehaviorHooks BlockBehaviors { get; }

    IModPlayerContextActionHooks ContextActions { get; }

    IModNetwork Network { get; }

    IModCommands Commands { get; }
}

public interface IModExtensions
{
    IDisposable Register<T>(string registryName, ResourceId id, T value) where T : class;

    bool TryGet<T>(string registryName, ResourceId id, out T? value) where T : class;
}

public interface IModCommands
{
    IModCommandAdapters Adapters { get; }

    IDisposable Register<TCommand>(
        ResourceId id,
        CommandDefinition<TCommand> definition)
        where TCommand : IGameCommand;
}

public interface IModCommandAdapters
{
    IDisposable Register<TBinding>(
        ResourceId id,
        TBinding binding)
        where TBinding : class, ICommandAdapterBinding;

    IReadOnlyList<RegisteredCommandAdapter<TBinding>> Get<TBinding>()
        where TBinding : class, ICommandAdapterBinding;

    bool TryGet<TBinding>(ResourceId id, out TBinding? binding)
        where TBinding : class, ICommandAdapterBinding;
}
