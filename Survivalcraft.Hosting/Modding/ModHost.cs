using Content.Packaging;

using Game.Modding.Data;

namespace Game.Modding;

public sealed class ModHost(RuntimeSide side = RuntimeSide.Client)
{
    public RuntimeSide Side { get; } = side;

    private readonly List<ModRuntime> _runtimes = [];

    private readonly HashSet<IDisposable> _disposedLifetimes = [];

    public ExtensionRegistry Extensions { get; } = new();

    public GameplayHooks Gameplay { get; } = new();

    public BlockBehaviorHooks BlockBehaviors { get; } = new();

    public PlayerContextActionHooks ContextActions { get; } = new();

    public ModNetworkHooks Network { get; } = new();

    public ModWorldStates Worlds { get; } = new();

    public CommandRegistry Commands { get; } = new();

    public IReadOnlyList<ModRuntime> Runtimes => _runtimes;

    public void LoadAndStart(IEnumerable<ModDescriptor> descriptors)
    {
        if (_runtimes.Count != 0)
        {
            throw new InvalidOperationException("Mod host has already been started.");
        }

        ArgumentNullException.ThrowIfNull(descriptors);
        var ownedDescriptors = new List<ModDescriptor>();
        try
        {
            foreach (var descriptor in descriptors) { ownedDescriptors.Add(descriptor); }

            var loadPlan = ModDependencyResolver.Resolve(ownedDescriptors);
            foreach (var descriptor in loadPlan)
            {
                if ((descriptor.Manifest.Scope, Side) is (ModScope.Server, RuntimeSide.Client)
                    or (ModScope.Client, RuntimeSide.Authority))
                {
                    throw new InvalidOperationException($"Mod {descriptor.Manifest.Id} cannot run on {Side}.");
                }

                var context = new ModContext(
                    Side,
                    descriptor.Manifest,
                    Extensions,
                    Gameplay,
                    BlockBehaviors,
                    ContextActions,
                    Network,
                    Worlds,
                    Commands
                );
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
            Worlds.Freeze();
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
                runtime.State is ModState.Configuring or ModState.Starting
            );
            if (failedRuntime is not null)
            {
                failedRuntime.State = ModState.Failed;
                failedRuntime.Failure = exception;
            }

            var cleanupErrors = new List<Exception>();
            CaptureCleanup(StopAll, cleanupErrors);
            DisposeLifetimes(ownedDescriptors, cleanupErrors);
            if (cleanupErrors.Count > 0)
            {
                throw new AggregateException(
                    "Mod startup and cleanup failed.",
                    new[] { exception }.Concat(cleanupErrors)
                );
            }

            throw;
        }
    }

    public void StopAll()
    {
        Worlds.EnsureWorldsReleased();
        var errors = new List<Exception>();
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
                    errors.Add(exception);
                }
            }

            var owner = runtime.Descriptor.Manifest.ModId;
            CaptureCleanup(() => Extensions.RemoveOwner(owner), errors);
            CaptureCleanup(() => Gameplay.RemoveOwner(owner), errors);
            CaptureCleanup(() => BlockBehaviors.RemoveOwner(owner), errors);
            CaptureCleanup(() => ContextActions.RemoveOwner(owner), errors);
            CaptureCleanup(() => Network.RemoveOwner(owner), errors);
            CaptureCleanup(() => Worlds.RemoveOwner(owner), errors);
            CaptureCleanup(() => Commands.RemoveOwner(owner), errors);
        }

        DisposeLifetimes(_runtimes.Select(runtime => runtime.Descriptor), errors);
        if (errors.Count > 0) { throw new AggregateException("Mod shutdown cleanup failed.", errors); }
    }

    private static void CaptureCleanup(Action cleanup, ICollection<Exception> errors)
    {
        try { cleanup(); }
        catch (Exception error) { errors.Add(error); }
    }

    private void DisposeLifetimes(IEnumerable<ModDescriptor> descriptors, List<Exception>? errors = null)
    {
        var failures = errors ?? [];
        foreach (var descriptor in descriptors.Reverse())
        {
            if (descriptor.Lifetime is { } lifetime && _disposedLifetimes.Add(lifetime))
            {
                CaptureCleanup(lifetime.Dispose, failures);
            }
        }

        if (errors is null && failures.Count > 0)
        {
            throw new AggregateException("Mod assembly lifetime cleanup failed.", failures);
        }
    }

    private sealed class ModContext(
        RuntimeSide side,
        ModManifest manifest,
        ExtensionRegistry extensions,
        GameplayHooks gameplay,
        BlockBehaviorHooks blockBehaviors,
        PlayerContextActionHooks contextActions,
        ModNetworkHooks network,
        ModWorldStates worlds,
        CommandRegistry commands
    ) : IModContext
    {
        public RuntimeSide Side { get; } = side;

        public ModManifest Manifest { get; } = manifest;

        public IModExtensions Extensions { get; } = new OwnedExtensions(manifest, extensions);

        public IModGameplayHooks Gameplay { get; } = gameplay.ForOwner(manifest.ModId);

        public IModBlockBehaviorHooks BlockBehaviors { get; } = blockBehaviors.ForOwner(manifest.ModId);

        public IModPlayerContextActionHooks ContextActions { get; } = contextActions.ForOwner(manifest.ModId);

        public IModNetwork Network => Manifest.Scope == ModScope.Both
            ? network.ForOwner(Manifest.ModId)
            : throw new InvalidOperationException(
                "Mod network messages require both client and authority participation."
            );

        public IModWorldStates Worlds { get; } = worlds.ForOwner(manifest.ModId);

        public IModCommands Commands { get; } = new OwnedCommands(manifest.ModId, commands);
    }

    private sealed class OwnedExtensions(ModManifest manifest, ExtensionRegistry extensions) : IModExtensions
    {
        public IDisposable Register<T>(string registryName, ResourceId id, T value) where T : class
        {
            if (manifest.Scope != ModScope.Both &&
                registryName is BlockExtensions.RegistryName or
                    BlockExtensions.DataRegistryName or XmlDataExtensions.DatabaseRegistryName or
                    XmlDataExtensions.RecipeRegistryName or XmlDataExtensions.ClothingRegistryName)
            {
                throw new InvalidOperationException($"Shared registry {registryName} requires a both-scope mod.");
            }

            return extensions.GetRegistry<T>(registryName).Register(manifest.ModId, id, value);
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
            CommandDefinition<TCommand> definition
        )
            where TCommand : IGameCommand
        {
            return commands.Register(owner, id, definition);
        }
    }

    private sealed class OwnedCommandPermissions(
        ModId owner,
        CommandPermissionRegistry permissions
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

    private sealed class OwnedCommandAdapters(
        ModId owner,
        CommandAdapterRegistry adapters
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
