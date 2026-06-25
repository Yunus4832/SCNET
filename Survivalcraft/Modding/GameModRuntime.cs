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
        BlockRuntimeCatalog blocks,
        GameDataCatalog data,
        ContentCatalog content,
        ModProfile effectiveProfile,
        IReadOnlyList<LoadedModInfo> loadedMods)
    {
        Host = host;
        Blocks = blocks;
        Data = data;
        Content = content;
        EffectiveProfile = effectiveProfile;
        LoadedMods = loadedMods;
        ModDataHash = ModProfileManager.ComputeDataHash(effectiveProfile);
    }

    public ModHost Host { get; }

    public BlockRuntimeCatalog Blocks { get; }

    public GameDataCatalog Data { get; }

    public ContentCatalog Content { get; }

    public ModProfile EffectiveProfile { get; }

    public string ModDataHash { get; }

    public IReadOnlyList<LoadedModInfo> LoadedMods { get; }

    public GameplayHooks Gameplay => Host.Gameplay;

    public BlockBehaviorHooks BlockBehaviors => Host.BlockBehaviors;

    public PlayerContextActionHooks ContextActions => Host.ContextActions;

    public ModNetworkHooks Network => Host.Network;

    public ModProfile? CreateServerRequiredProfile()
    {
        var packages = EffectiveProfile.Packages
            .Select(package => new ModPackageRequirement
            {
                ModId = package.ModId,
                Version = package.Version,
                PackageHash = package.PackageHash
            })
            .ToList();
        var repositoryUrl = string.IsNullOrWhiteSpace(EffectiveProfile.RepositoryUrl)
            ? SettingsManager.Current.ModServerAddress
            : EffectiveProfile.RepositoryUrl;

        if (packages.Count == 0 || string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        return new ModProfile
        {
            Id = "server",
            RepositoryUrl = repositoryUrl,
            Packages = packages
        };
    }

    public static GameModRuntime Start(IEnumerable<ModDescriptor>? externalMods = null)
    {
        return StartFromDescriptors(externalMods ?? [], null);
    }

    public static GameModRuntime StartFromProfile(
        ModProfile profile,
        string localRepositoryPath,
        ModSide hostSide,
        Action<string>? log = null,
        bool fallbackToBuiltInOnFailure = false)
    {
        var sources = ModProfileResolver.ResolveRequiredPackages(profile, localRepositoryPath, log);
        return StartFromPackageSources(sources, hostSide, fallbackToBuiltInOnFailure, profile);
    }

    private static GameModRuntime StartFromPackageSources(
        IEnumerable<ModPackageSource> sources,
        ModSide hostSide,
        bool fallbackToBuiltInOnFailure,
        ModProfile effectiveProfile)
    {
        var materializedSources = sources.ToArray();
        var externalMods = ModPackageCatalog.CreateLoadPlan(materializedSources, hostSide);
        if (!fallbackToBuiltInOnFailure)
        {
            return StartFromDescriptors(externalMods, effectiveProfile);
        }

        try
        {
            var runtime = StartFromDescriptors(externalMods, effectiveProfile);
            Log.Information(
                "Loaded mods for {0}: {1}",
                hostSide,
                string.Join(", ", runtime.Host.Runtimes.Select(item => item.Descriptor.Manifest.Id)));
            return runtime;
        }
        catch
        {
            foreach (var descriptor in externalMods)
            {
                descriptor.Lifetime?.Dispose();
            }

            Log.Error("Failed to load external mods from package sources; continuing with built-in content only.");
            return Start();
        }
    }

    public void InitializeBlocks()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        InitializeAssets();
        BlocksManager.Initialize(Blocks, Data.BuildClothing());
        var externalBlocks = Blocks.ById.Values
            .Where(entry => entry.Id.Namespace != new ModId("game"))
            .OrderBy(entry => entry.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        foreach (var entry in externalBlocks)
        {
            Log.Information(
                "Initialized mod block {0} at runtime index {1} ({2}), name={3}, category={4}, order={5}, creativeValues={6}.",
                entry.Id,
                entry.RuntimeIndex,
                entry.Block.GetType().FullName ?? entry.Block.GetType().Name,
                entry.Block.DisplayName,
                entry.Block.Category,
                entry.Block.DisplayOrder,
                string.Join(",", entry.Block.GetCreativeValues()));
        }
    }

    public void InitializeDatabase()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        InitializeAssets();
        DatabaseManager.Initialize();
        DatabaseManager.DatabaseNodeField = Data.BuildDatabase();
        DatabaseManager.LoadDataBaseFromXml(DatabaseManager.DatabaseNodeField);
    }

    public void InitializeCraftingRecipes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        InitializeAssets();
        CraftingRecipesManager.Initialize(Data.BuildRecipes());
    }

    public void InitializeContentData()
    {
        InitializeDatabase();
        InitializeBlocks();
        InitializeCraftingRecipes();
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
            descriptor.PackageHash);
    }

    private static GameModRuntime StartFromDescriptors(IEnumerable<ModDescriptor> descriptors,
        ModProfile? effectiveProfile)
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
            var blocks = BlockRuntimeCatalog.Compile(blockRegistry, dataRegistry);
            var data = GameDataCatalog.Compile(host.Extensions);
            var content = ContentCatalog.Compile(host.Extensions);
            var loadedMods = CreateLoadedModInfos(host);
            return new GameModRuntime(
                host,
                blocks,
                data,
                content,
                CreateEffectiveProfile(loadedMods, effectiveProfile),
                loadedMods);
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
                    host.Network)))
            .ToArray();
    }

    private static ModProfile CreateEffectiveProfile(IReadOnlyList<LoadedModInfo> loadedMods, ModProfile? profile)
    {
        if (profile is not null)
        {
            return new ModProfile
            {
                Id = profile.Id,
                RepositoryUrl = profile.RepositoryUrl,
                Packages = profile.Packages
                    .Select(package => new ModPackageRequirement
                    {
                        ModId = package.ModId,
                        Version = package.Version,
                        PackageHash = package.PackageHash
                    })
                    .ToList()
            };
        }

        return new ModProfile
        {
            Id = "runtime",
            RepositoryUrl = SettingsManager.Current.ModServerAddress,
            Packages = loadedMods
                .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageHash))
                .Select(mod => new ModPackageRequirement
                {
                    ModId = mod.PackageName,
                    Version = mod.Version,
                    PackageHash = mod.PackageHash
                })
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
        ModNetworkHooks network)
    {
        var owner = descriptor.Manifest.ModId;
        var lines = new List<string> { $"owner:{owner}" };
        if (descriptor.PackageHash is not null)
        {
            lines.Add($"package:{descriptor.PackageHash}");
        }

        lines.AddRange(blocks.Entries
            .Where(entry => entry.Key.Namespace == owner)
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => $"block:{entry.Key}:{entry.Value.LegacyIndex}"));
        lines.AddRange(blockData.Entries
            .Where(entry => entry.Key.Namespace == owner)
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => $"block_data:{entry.Key}"));
        lines.AddRange(database.Entries
            .Where(entry => entry.Key.Namespace == owner)
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => $"database:{entry.Key}:{entry.Value.Mode}"));
        lines.AddRange(recipes.Entries
            .Where(entry => entry.Key.Namespace == owner)
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => $"recipes:{entry.Key}:{entry.Value.Mode}"));
        lines.AddRange(clothing.Entries
            .Where(entry => entry.Key.Namespace == owner)
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => $"clothing:{entry.Key}:{entry.Value.Mode}"));
        lines.AddRange(content.Entries
            .Where(entry => entry.Key.Namespace == owner)
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => $"content:{entry.Key}:{entry.Value.RelativePath}"));
        lines.AddRange(network.Registrations
            .Where(entry => entry.Owner == owner)
            .OrderBy(entry => entry.MessageType, StringComparer.Ordinal)
            .Select(entry => $"network:{entry.Owner}:{entry.MessageType}"));
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
