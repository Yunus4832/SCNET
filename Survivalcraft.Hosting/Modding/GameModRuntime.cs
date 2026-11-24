using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.Modding.Content;
using Game.Modding.Data;

namespace Game.Modding;

public sealed class GameModRuntime : IDisposable
{
    private bool _disposed;

    private GameModRuntime(
        ModHost host,
        NamespacedRegistry<BlockRegistration> blockRegistry,
        NamespacedRegistry<BlockDataRegistration> blockDataRegistry,
        GameDataCatalog data,
        ContentCatalog content,
        ModProfile loadedProfile,
        IReadOnlyList<LoadedModInfo> loadedMods,
        ModLoadPlan? plan
    )
    {
        Host = host;
        _blockRegistry = blockRegistry;
        _blockDataRegistry = blockDataRegistry;
        Data = data;
        Content = content;
        LoadedProfile = loadedProfile;
        LoadedMods = loadedMods;
        FullProfile = plan?.FullProfile ?? loadedProfile;
        _clientProfile = plan?.ClientProfile ?? loadedProfile;
        ModDataHash = ModProfileFingerprint.Compute(_clientProfile);
    }

    public ModHost Host { get; }

    public RuntimeSide Side => Host.Side;

    private readonly NamespacedRegistry<BlockRegistration> _blockRegistry;

    private readonly NamespacedRegistry<BlockDataRegistration> _blockDataRegistry;

    private BlockContentCatalog? _previewCatalog;

    private bool _contentDataInitialized;

    public BlockContentCatalog PreviewCatalog
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Side != RuntimeSide.Client)
            {
                throw new InvalidOperationException("Preview catalogs belong to client mod runtimes.");
            }

            if (!_contentDataInitialized)
            {
                throw new InvalidOperationException("Content data has not been initialized.");
            }

            return _previewCatalog ??= BuildCatalog(BlockIndexMapping.Empty);
        }
    }

    private static readonly ConditionalWeakTable<WorldRuntimeContext, GameModRuntime> _worldOwners = new();

    /// <summary>返回创建该世界目录并持有其注册项的模组宿主。</summary>
    public static GameModRuntime For(WorldRuntimeContext world) => _worldOwners.TryGetValue(world, out var runtime)
        ? runtime
        : throw new InvalidOperationException("The world has no mod runtime.");

    private readonly Dictionary<WorldRuntimeContext, BlockContentCatalog> _worldCatalogs = [];

    public IReadOnlyCollection<BlockContentCatalog> WorldCatalogs
    {
        get
        {
            lock (Host.Worlds.LifecycleGate) { return _worldCatalogs.Values.ToArray(); }
        }
    }

    public BlockContentCatalog CreateWorldCatalog(
        WorldRuntimeContext runtime,
        BlockIndexMapping mapping,
        bool requireExactMapping = false
    )
    {
        lock (Host.Worlds.LifecycleGate)
        {
            EnsureCanBindWorld();
            ArgumentNullException.ThrowIfNull(runtime);
            ObjectDisposedException.ThrowIf(runtime.IsClosed, runtime);
            EnsureWorldSide(runtime);
            var catalog = BuildCatalog(mapping, requireExactMapping);
            try
            {
                AdoptWorldCatalog(runtime, catalog);
                return catalog;
            }
            catch
            {
                catalog.Dispose();
                throw;
            }
        }
    }

    internal void AdoptWorldCatalog(WorldRuntimeContext runtime, BlockContentCatalog catalog)
    {
        lock (Host.Worlds.LifecycleGate)
        {
            EnsureCanBindWorld();
            EnsureWorldSide(runtime);
            _ = new WorldContentBinding(runtime, catalog, () => ReleaseWorldCatalog(runtime));
            _ = new WorldRuleHooks(runtime, Gameplay, BlockBehaviors);
            _worldCatalogs.Add(runtime, catalog);
            _worldOwners.Add(runtime, this);
        }
    }

    private void EnsureWorldSide(WorldRuntimeContext runtime)
    {
        if (runtime.Side != Side)
        {
            throw new InvalidOperationException($"A {Side} mod runtime cannot own a {runtime.Side} world.");
        }
    }

    private void EnsureCanBindWorld()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Host.Worlds.IsRetiring)
        {
            throw new InvalidOperationException("The mod environment is being replaced.");
        }
    }

    internal void EnsureAlive()
    {
        lock (Host.Worlds.LifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }

    internal void BeginRetirement()
    {
        lock (Host.Worlds.LifecycleGate)
        {
            EnsureCanBindWorld();
            if (_worldCatalogs.Count != 0)
            {
                throw new InvalidOperationException(
                    "Release all world catalogs before replacing their mod environment."
                );
            }

            Host.Worlds.EnsureWorldsReleased();
            Host.Worlds.IsRetiring = true;
        }
    }

    internal void CancelRetirement()
    {
        lock (Host.Worlds.LifecycleGate)
        {
            if (!_disposed) { Host.Worlds.IsRetiring = false; }
        }
    }

    private void ReleaseWorldCatalog(WorldRuntimeContext runtime)
    {
        lock (Host.Worlds.LifecycleGate)
        {
            if (_worldCatalogs.Remove(runtime, out var catalog))
            {
                _worldOwners.Remove(runtime);
                catalog.Dispose();
            }
        }
    }

    private readonly Dictionary<string, Type> _resolvedTypes = new(StringComparer.Ordinal);

    /// <summary>在宿主和当前运行时的模组程序集中解析模板类型。</summary>
    public Type? FindType(string name, bool throwIfNotFound = true)
    {
        lock (_resolvedTypes)
        {
            if (_resolvedTypes.TryGetValue(name, out var type)) { return type; }

            type = System.Runtime.Loader.AssemblyLoadContext.Default.Assemblies
                       .Select(assembly => assembly.GetType(name))
                       .FirstOrDefault(candidate => candidate is not null) ??
                   Host.Runtimes.Select(runtime => runtime.Descriptor.Entrypoints?.FindLoadedType(name))
                       .SingleOrDefault(candidate => candidate is not null);
            if (type is not null) { _resolvedTypes.Add(name, type); }

            return type ?? (throwIfNotFound ? throw new TypeLoadException(name) : null);
        }
    }

    private BlockContentCatalog BuildCatalog(
        BlockIndexMapping mapping,
        bool requireExactMapping = false
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_blockRegistry.IsFrozen || !_blockDataRegistry.IsFrozen)
        {
            throw new InvalidOperationException("World content requires frozen mod registrations.");
        }

        var dataEntries = _blockDataRegistry.Entries
            .Select(entry => new BlockDataEntry(entry.Key, entry.Value.Read())).ToArray();
        var registry = BlockRuntimeCatalog.Compile(_blockRegistry.Entries, dataEntries, mapping, requireExactMapping);
        var catalog = new BlockContentCatalog(registry);
        try
        {
            var collision = Content.LoadCollisionGeometry(
                catalog.RegisteredBlocks.Select(block => block.CollisionModelAsset).OfType<ResourceId>()
            );
            catalog.Initialize(collision, Data.BuildClothing(), Data.BuildRecipes(), Database);
            return catalog;
        }
        catch
        {
            catalog.Dispose();
            throw;
        }
    }

    public GameDataCatalog Data { get; }

    public ContentCatalog Content { get; }

    private readonly ModProfile _clientProfile;

    public ModProfile FullProfile { get; }

    public GameDatabase Database { get; private set; } = null!;

    public ModProfile LoadedProfile { get; }

    public string ModDataHash { get; }

    public IReadOnlyList<LoadedModInfo> LoadedMods { get; }

    public GameplayHooks Gameplay => Host.Gameplay;

    public BlockBehaviorHooks BlockBehaviors => Host.BlockBehaviors;

    public PlayerContextActionHooks ContextActions => Host.ContextActions;

    public ModNetworkHooks Network => Host.Network;

    public CommandRegistry Commands => Host.Commands;

    public ModProfile CreateServerRequiredProfile()
    {
        var packages = _clientProfile.Packages
            .Select(package => new ModPackageRequirement
                {
                    ModId = package.ModId,
                    Version = package.Version,
                    PackageHash = package.PackageHash
                }
            )
            .ToList();
        var profile = new ModProfile
        {
            Id = "server",
            Packages = packages
        };
        ModProfileValidation.Validate(profile);
        return profile;
    }

    public static GameModRuntime Start(
        IEnumerable<ModDescriptor>? externalMods = null,
        Action<IModCommands, ModId>? registerAppCommands = null,
        RuntimeSide side = RuntimeSide.Client
    )
    {
        return StartFromDescriptors(externalMods ?? [], null, registerAppCommands, side);
    }

    public static GameModRuntime StartFromProfile(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null,
        Action<IModCommands, ModId>? registerAppCommands = null,
        RuntimeSide side = RuntimeSide.Client
    )
    {
        return StartFromPlan(ModLoadPlan.Read(profile, localRepositoryPath, log), side, registerAppCommands);
    }

    public static GameModRuntime StartFromPlan(
        ModLoadPlan plan,
        RuntimeSide side,
        Action<IModCommands, ModId>? registerAppCommands = null
    )
    {
        var sources = plan.GetPackages(side);
        var externalMods = ModPackageCatalog.Discover(sources).Select(package => package.CreateDescriptor());
        return StartFromDescriptors(externalMods, plan, registerAppCommands, side);
    }

    public void InitializeDatabase()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Database = DatabaseManager.Create(Data.BuildDatabase(), FindType);
    }

    public void InitializeContentData()
    {
        InitializeDatabase();
        _contentDataInitialized = true;
    }

    public void Dispose()
    {
        lock (Host.Worlds.LifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            if (_worldCatalogs.Keys.Any(runtime => runtime.HasProjectResources))
            {
                throw new InvalidOperationException("Dispose all worlds before unloading their mod environment.");
            }

            Host.Worlds.EnsureWorldsReleased();
            _disposed = true;
            var errors = new List<Exception>();
            foreach (var runtime in _worldCatalogs.Keys.ToArray())
            {
                try { runtime.Close(); }
                catch (Exception error) { errors.Add(error); }
            }

            try { _previewCatalog?.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            finally { _previewCatalog = null; }

            lock (_resolvedTypes) { _resolvedTypes.Clear(); }

            try { Host.StopAll(); }
            catch (Exception error) { errors.Add(error); }

            if (errors.Count > 0)
            {
                throw new AggregateException("Could not release every mod environment resource.", errors);
            }
        }
    }

    private static LoadedModInfo CreateLoadedModInfo(ModDescriptor descriptor, string fingerprint)
    {
        return new LoadedModInfo(
            descriptor.Manifest.Name,
            descriptor.Manifest.Id,
            descriptor.Manifest.Version,
            fingerprint,
            descriptor.PackageHash
        );
    }

    private static GameModRuntime StartFromDescriptors(
        IEnumerable<ModDescriptor> descriptors,
        ModLoadPlan? plan,
        Action<IModCommands, ModId>? registerAppCommands,
        RuntimeSide side
    )
    {
        var allDescriptors = descriptors.Prepend(
            BuiltInContentMod.CreateDescriptor(registerAppCommands, side == RuntimeSide.Client)
        );

        var host = new ModHost(side);
        try
        {
            host.Extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName);
            host.LoadAndStart(allDescriptors);
            var blockRegistry = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
            var dataRegistry = host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName);
            var data = GameDataCatalog.Compile(host.Extensions);
            var content = ContentCatalog.Compile(host.Extensions);
            var loadedMods = CreateLoadedModInfos(host);
            return new GameModRuntime(
                host,
                blockRegistry,
                dataRegistry,
                data,
                content,
                CreateLoadedProfile(
                    loadedMods,
                    side == RuntimeSide.Client ? plan?.ClientProfile : plan?.AuthorityProfile
                ),
                loadedMods,
                plan
            );
        }
        catch
        {
            host.StopAll();
            throw;
        }
    }

    private static IReadOnlyList<LoadedModInfo> CreateLoadedModInfos(ModHost host)
    {
        var blockRegistry = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
        var blockDataRegistry = host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName);
        var databaseRegistry = host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName);
        var recipeRegistry = host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.RecipeRegistryName);
        var clothingRegistry = host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.ClothingRegistryName);
        var contentRegistry = host.Extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName);

        return host.Runtimes
            .Select(runtime => CreateLoadedModInfo(
                    runtime.Descriptor,
                    BuildFingerprint(
                        runtime.Descriptor,
                        blockRegistry,
                        blockDataRegistry,
                        databaseRegistry,
                        recipeRegistry,
                        clothingRegistry,
                        contentRegistry,
                        host.Network
                    )
                )
            )
            .ToArray();
    }

    private static ModProfile CreateLoadedProfile(IReadOnlyList<LoadedModInfo> loadedMods, ModProfile? profile)
    {
        if (profile is not null)
        {
            return new ModProfile
            {
                Id = profile.Id,
                Packages = profile.Packages
                    .Select(package => new ModPackageRequirement
                        {
                            ModId = package.ModId,
                            Version = package.Version,
                            PackageHash = package.PackageHash
                        }
                    )
                    .ToList()
            };
        }

        return new ModProfile
        {
            Id = "runtime",
            Packages = loadedMods
                .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageHash))
                .Select(mod => new ModPackageRequirement
                    {
                        ModId = mod.PackageName,
                        Version = mod.Version,
                        PackageHash = mod.PackageHash!
                    }
                )
                .ToList()
        };
    }

    private static string BuildFingerprint(
        ModDescriptor descriptor,
        NamespacedRegistry<BlockRegistration> blocks,
        NamespacedRegistry<BlockDataRegistration> blockData,
        NamespacedRegistry<XmlDataRegistration> database,
        NamespacedRegistry<XmlDataRegistration> recipes,
        NamespacedRegistry<XmlDataRegistration> clothing,
        NamespacedRegistry<ContentRegistration> content,
        ModNetworkHooks network
    )
    {
        var owner = descriptor.Manifest.ModId;
        var lines = new List<string> { $"owner:{owner}" };
        if (descriptor.PackageHash is not null)
        {
            lines.Add($"package:{descriptor.PackageHash}");
        }

        lines.AddRange(
            blocks.Entries
                .Where(entry => entry.Key.Namespace == owner)
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => $"block:{entry.Key}")
        );
        lines.AddRange(
            blockData.Entries
                .Where(entry => entry.Key.Namespace == owner)
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => $"block_data:{entry.Key}")
        );
        lines.AddRange(
            database.Entries
                .Where(entry => entry.Key.Namespace == owner)
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => $"database:{entry.Key}:{entry.Value.Mode}")
        );
        lines.AddRange(
            recipes.Entries
                .Where(entry => entry.Key.Namespace == owner)
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => $"recipes:{entry.Key}:{entry.Value.Mode}")
        );
        lines.AddRange(
            clothing.Entries
                .Where(entry => entry.Key.Namespace == owner)
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => $"clothing:{entry.Key}:{entry.Value.Mode}")
        );
        lines.AddRange(
            content.Entries
                .Where(entry => entry.Key.Namespace == owner && !entry.Value.IsClientAsset)
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => $"content:{entry.Key}:{entry.Value.RelativePath}")
        );
        lines.AddRange(
            network.Registrations
                .Where(entry => entry.Owner == owner)
                .OrderBy(entry => entry.MessageType, StringComparer.Ordinal)
                .Select(entry => $"network:{entry.Owner}:{entry.MessageType}")
        );
        return HashUtils.ComputeMd5(string.Join('\n', lines));
    }
}

public sealed record LoadedModInfo(
    string Name,
    string PackageName,
    string Version,
    string ResourcesMd5,
    string? PackageHash
);
