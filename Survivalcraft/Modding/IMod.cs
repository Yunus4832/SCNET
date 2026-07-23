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
    IDisposable Register(ResourceId id, GameCommand command);
}
