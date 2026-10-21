using Game.Commands;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;

namespace Game.Modding;

public sealed class GameModRuntime : IDisposable
{
    private bool _disposed;

    private bool _assetsInitialized;

    private GameModRuntime(
        ModHost host,
        NamespacedRegistry<BlockRegistration> blockRegistry,
        NamespacedRegistry<BlockDataRegistration> blockDataRegistry,
        GameDataCatalog data,
        ContentCatalog content,
        ModProfile effectiveProfile,
        IReadOnlyList<LoadedModInfo> loadedMods
    )
    {
        Host = host;
        _blockRegistry = blockRegistry;
        _blockDataRegistry = blockDataRegistry;
        Data = data;
        Content = content;
        EffectiveProfile = effectiveProfile;
        LoadedMods = loadedMods;
        ModDataHash = ModProfileManager.ComputeDataHash(effectiveProfile);
    }

    public ModHost Host { get; }

    private readonly NamespacedRegistry<BlockRegistration> _blockRegistry;

    private readonly NamespacedRegistry<BlockDataRegistration> _blockDataRegistry;

    private BlockContentCatalog? _previewCatalog;

    public BlockContentCatalog PreviewCatalog => _previewCatalog ??
                                                 throw new InvalidOperationException(
                                                     "Preview content has not been initialized."
                                                 );

    public BlockContentCatalog? WorldCatalog { get; private set; }

    public BlockContentCatalog RequireWorldCatalog() =>
        WorldCatalog ?? throw new InvalidOperationException("No world block catalog is available.");

    public BlockContentCatalog CreateWorldCatalog(BlockIndexMapping mapping, bool requireExactMapping = false)
    {
        if (WorldCatalog is not null)
        {
            throw new InvalidOperationException("A world catalog is already active.");
        }

        var catalog = BuildCatalog(mapping, requireExactMapping);
        WorldCatalog = catalog;
        return catalog;
    }

    public void ReleaseWorldCatalog()
    {
        var catalog = WorldCatalog;
        WorldCatalog = null;
        catalog?.Dispose();
    }

    private BlockContentCatalog BuildCatalog(BlockIndexMapping mapping, bool requireExactMapping = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        InitializeAssets();
        var registry = BlockRuntimeCatalog.Compile(_blockRegistry, _blockDataRegistry, mapping, requireExactMapping);
        var catalog = new BlockContentCatalog(registry);
        try
        {
            catalog.Initialize(Data.BuildClothing(), Data.BuildRecipes());
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

    public ModProfile EffectiveProfile { get; }

    public string ModDataHash { get; }

    public IReadOnlyList<LoadedModInfo> LoadedMods { get; }

    public GameplayHooks Gameplay => Host.Gameplay;

    public BlockBehaviorHooks BlockBehaviors => Host.BlockBehaviors;

    public PlayerContextActionHooks ContextActions => Host.ContextActions;

    public ModNetworkHooks Network => Host.Network;

    public CommandRegistry Commands => Host.Commands;

    public ModProfile CreateServerRequiredProfile()
    {
        var packages = EffectiveProfile.Packages
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

    public static GameModRuntime Start(IEnumerable<ModDescriptor>? externalMods = null)
    {
        return StartFromDescriptors(externalMods ?? [], null);
    }

    public static GameModRuntime StartFromProfile(
        ModProfile profile,
        string localRepositoryPath,
        ModSide hostSide,
        Action<string>? log = null
    )
    {
        var sources = ModProfileResolver.ResolveRequiredPackages(profile, localRepositoryPath, log);
        var externalMods = ModPackageCatalog.CreateLoadPlan(sources, hostSide);
        return StartFromDescriptors(externalMods, profile);
    }

    public void InitializeDatabase()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        InitializeAssets();
        DatabaseManager.Initialize();
        DatabaseManager.DatabaseNodeField = Data.BuildDatabase();
        DatabaseManager.LoadDataBaseFromXml(DatabaseManager.DatabaseNodeField);
    }

    public void InitializeContentData()
    {
        InitializeDatabase();
        if (RunMode.Value == RunModeType.Gui)
        {
            _previewCatalog ??= BuildCatalog(BlockIndexMapping.Empty);
        }
    }

    public void InitializeAssets()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_assetsInitialized)
        {
            return;
        }

        Content.Install();
        _assetsInitialized = true;
    }

    public void RefreshRecipeDescriptions()
    {
        var recipes = Data.BuildRecipes();
        _previewCatalog?.Recipes.Initialize(recipes);
        WorldCatalog?.Recipes.Initialize(recipes);
    }

    public void InitializeLanguage(string languageType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        InitializeAssets();
        Content.InitializeLanguage(languageType);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseWorldCatalog();
        _previewCatalog?.Dispose();
        _previewCatalog = null;
        Content.Uninstall();
        Host.StopAll();
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
        ModProfile? effectiveProfile
    )
    {
        var allDescriptors = new List<ModDescriptor> { BuiltInContentMod.CreateDescriptor() };
        allDescriptors.AddRange(descriptors);

        var host = new ModHost();
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
                CreateEffectiveProfile(loadedMods, effectiveProfile),
                loadedMods
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

    private static ModProfile CreateEffectiveProfile(IReadOnlyList<LoadedModInfo> loadedMods, ModProfile? profile)
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
                .Where(entry => entry.Key.Namespace == owner)
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
