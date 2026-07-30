using Game.Commands;

namespace Game.Modding;

public sealed class ModHost
{
    private readonly List<ModRuntime> _runtimes = [];
    private readonly HashSet<IDisposable> _disposedLifetimes = [];

    public ExtensionRegistry Extensions { get; } = new();

    public GameplayHooks Gameplay { get; } = new();

    public BlockBehaviorHooks BlockBehaviors { get; } = new();

    public PlayerContextActionHooks ContextActions { get; } = new();

    public ModNetworkHooks Network { get; } = new();

    public CommandRegistry Commands { get; } = new();

    public IReadOnlyList<ModRuntime> Runtimes => _runtimes;

    public void LoadAndStart(IEnumerable<ModDescriptor> descriptors)
    {
        if (_runtimes.Count != 0)
        {
            throw new InvalidOperationException("Mod host has already been started.");
        }

        var loadPlan = ModDependencyResolver.Resolve(descriptors);
        try
        {
            foreach (var descriptor in loadPlan)
            {
                var context = new ModContext(
                    descriptor.Manifest,
                    Extensions,
                    Gameplay,
                    BlockBehaviors,
                    ContextActions,
                    Network,
                    Commands);
                var runtime = new ModRuntime(descriptor, descriptor.Factory(), context);
                _runtimes.Add(runtime);
                runtime.State = ModState.Configuring;
                runtime.Instance.Configure(context);
                runtime.State = ModState.Configured;
            }

            Extensions.Freeze();
            Gameplay.Freeze();
            BlockBehaviors.Freeze();
            ContextActions.Freeze();
            Network.Freeze();
            Commands.Freeze();
            foreach (var runtime in _runtimes)
            {
                runtime.State = ModState.Starting;
                runtime.Instance.Start(runtime.Context);
                runtime.State = ModState.Started;
            }
        }
        catch (Exception exception)
        {
            var failedRuntime = _runtimes.LastOrDefault(runtime =>
                runtime.State is ModState.Configuring or ModState.Starting);
            if (failedRuntime is not null)
            {
                failedRuntime.State = ModState.Failed;
                failedRuntime.Failure = exception;
            }

            StopAll();
            DisposeLifetimes(loadPlan);
            throw;
        }
    }

    public void StopAll()
    {
        foreach (var runtime in _runtimes.AsEnumerable().Reverse())
        {
            if (runtime.State is ModState.Started or ModState.Starting)
            {
                try
                {
                    runtime.State = ModState.Stopping;
                    runtime.Instance.Stop();
                    runtime.State = ModState.Stopped;
                }
                catch (Exception exception)
                {
                    runtime.State = ModState.Failed;
                    runtime.Failure = exception;
                }
            }

            Extensions.RemoveOwner(runtime.Descriptor.Manifest.ModId);
            Gameplay.RemoveOwner(runtime.Descriptor.Manifest.ModId);
            BlockBehaviors.RemoveOwner(runtime.Descriptor.Manifest.ModId);
            ContextActions.RemoveOwner(runtime.Descriptor.Manifest.ModId);
            Network.RemoveOwner(runtime.Descriptor.Manifest.ModId);
            Commands.RemoveOwner(runtime.Descriptor.Manifest.ModId);
        }

        DisposeLifetimes(_runtimes.Select(runtime => runtime.Descriptor));
    }

    private void DisposeLifetimes(IEnumerable<ModDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors.Reverse())
        {
            if (descriptor.Lifetime is { } lifetime && _disposedLifetimes.Add(lifetime))
            {
                lifetime.Dispose();
            }
        }
    }

    private sealed class ModContext(
        ModManifest manifest,
        ExtensionRegistry extensions,
        GameplayHooks gameplay,
        BlockBehaviorHooks blockBehaviors,
        PlayerContextActionHooks contextActions,
        ModNetworkHooks network,
        CommandRegistry commands) : IModContext
    {
        public ModManifest Manifest { get; } = manifest;

        public IModExtensions Extensions { get; } = new OwnedExtensions(manifest.ModId, extensions);

        public IModGameplayHooks Gameplay { get; } = gameplay.ForOwner(manifest.ModId);

        public IModBlockBehaviorHooks BlockBehaviors { get; } = blockBehaviors.ForOwner(manifest.ModId);

        public IModPlayerContextActionHooks ContextActions { get; } = contextActions.ForOwner(manifest.ModId);

        public IModNetwork Network { get; } = network.ForOwner(manifest.ModId);

        public IModCommands Commands { get; } = new OwnedCommands(manifest.ModId, commands);
    }

    private sealed class OwnedExtensions(ModId owner, ExtensionRegistry extensions) : IModExtensions
    {
        public IDisposable Register<T>(string registryName, ResourceId id, T value) where T : class
        {
            return extensions.GetRegistry<T>(registryName).Register(owner, id, value);
        }

        public bool TryGet<T>(string registryName, ResourceId id, out T? value) where T : class
        {
            return extensions.GetRegistry<T>(registryName).TryGet(id, out value);
        }
    }

    private sealed class OwnedCommands(ModId owner, CommandRegistry commands) : IModCommands
    {
        public IModCommandAdapters Adapters { get; } =
            new OwnedCommandAdapters(owner, commands.Adapters);

        public IModCommandPermissions Permissions { get; } =
            new OwnedCommandPermissions(owner, commands.Permissions);

        public IDisposable Register<TCommand>(
            ResourceId id,
            CommandDefinition<TCommand> definition)
            where TCommand : IGameCommand
        {
            return commands.Register(owner, id, definition);
        }
    }

    private sealed class OwnedCommandPermissions(
        ModId owner,
        CommandPermissionRegistry permissions) : IModCommandPermissions
    {
        public IDisposable Register(
            ResourceId id,
            CommandPermissionDefinition definition)
        {
            return permissions.Register(owner, id, definition);
        }
    }

    private sealed class OwnedCommandAdapters(
        ModId owner,
        CommandAdapterRegistry adapters) : IModCommandAdapters
    {
        public IDisposable Register<TBinding>(
            ResourceId id,
            TBinding binding)
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
