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
        ContentCatalog content)
    {
        Host = host;
        Blocks = blocks;
        Data = data;
        Content = content;
    }

    public ModHost Host { get; }

    public BlockRuntimeCatalog Blocks { get; }

    public GameDataCatalog Data { get; }

    public ContentCatalog Content { get; }

    public GameplayHooks Gameplay => Host.Gameplay;

    public IReadOnlyList<LoadedModInfo> GetLoadedMods()
    {
        var blockRegistry = Host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
        var blockDataRegistry = Host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName);
        var databaseRegistry = Host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName);
        var recipeRegistry = Host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.RecipeRegistryName);
        var clothingRegistry = Host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.ClothingRegistryName);
        var contentRegistry = Host.Extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName);

        return Host.Runtimes
            .Select(runtime => CreateLoadedModInfo(
                runtime.Descriptor.Manifest,
                BuildFingerprint(
                    runtime.Descriptor,
                    blockRegistry,
                    blockDataRegistry,
                    databaseRegistry,
                    recipeRegistry,
                    clothingRegistry,
                    contentRegistry)))
            .ToArray();
    }

    public static GameModRuntime Start(IEnumerable<ModDescriptor>? externalMods = null)
    {
        var descriptors = new List<ModDescriptor> { BuiltInContentMod.CreateDescriptor() };
        if (externalMods is not null)
        {
            descriptors.AddRange(externalMods);
        }

        var host = new ModHost();
        try
        {
            host.Extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName);
            host.LoadAndStart(descriptors);
            var blockRegistry = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
            var dataRegistry = host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName);
            var blocks = BlockRuntimeCatalog.Compile(blockRegistry, dataRegistry);
            var data = GameDataCatalog.Compile(host.Extensions);
            var content = ContentCatalog.Compile(host.Extensions);
            return new GameModRuntime(host, blocks, data, content);
        }
        catch
        {
            host.StopAll();
            throw;
        }
    }

    public static GameModRuntime StartFromDirectory(string directoryPath, ModSide hostSide)
    {
        var externalMods = ModPackageCatalog.CreateLoadPlan(
            directoryPath,
            hostSide,
            ModSelectionSettings.DisabledPackages);
        try
        {
            return Start(externalMods);
        }
        catch
        {
            foreach (var descriptor in externalMods)
            {
                descriptor.Lifetime?.Dispose();
            }

            Log.Error(
                "Failed to load external mods from {0}; continuing with built-in content only.",
                directoryPath);
            return Start();
        }
    }

    public static GameModRuntime StartFromStorageDirectory(string directoryPath, ModSide hostSide)
    {
        if (!Storage.DirectoryExists(directoryPath))
        {
            Storage.CreateDirectory(directoryPath);
        }

        var packageNames = Storage.ListFileNames(directoryPath)
            .Where(name =>
                Storage.GetExtension(name).Equals(ModPackage.FileExtension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Log.Information(
            "Scanning mod directory {0} for {1}: found {2} package(s).",
            Storage.GetSystemPath(directoryPath),
            hostSide,
            packageNames.Length);
        foreach (var packageName in packageNames)
        {
            Log.Information("Discovered mod package: {0}", packageName);
        }

        var sources = packageNames
            .Select(name =>
            {
                var path = Storage.CombinePaths(directoryPath, name);
                return new ModPackageSource(path, () => Storage.OpenFile(path, OpenFileMode.Read));
            })
            .ToArray();
        var externalMods = ModPackageCatalog.CreateLoadPlan(
            sources,
            hostSide,
            ModSelectionSettings.DisabledPackages);
        try
        {
            var runtime = Start(externalMods);
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

            Log.Error(
                "Failed to load external mods from {0}; continuing with built-in content only.",
                Storage.GetSystemPath(directoryPath));
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

    private static LoadedModInfo CreateLoadedModInfo(ModManifest manifest, string fingerprint)
    {
        return new LoadedModInfo(
            manifest.Name,
            manifest.Id,
            manifest.Version,
            fingerprint);
    }

    private static string BuildFingerprint(
        ModDescriptor descriptor,
        NamespacedRegistry<BlockRegistration> blocks,
        NamespacedRegistry<BlockDataRegistration> blockData,
        NamespacedRegistry<XmlDataRegistration> database,
        NamespacedRegistry<XmlDataRegistration> recipes,
        NamespacedRegistry<XmlDataRegistration> clothing,
        NamespacedRegistry<ContentRegistration> content)
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
        return HashUtils.ComputeMd5(string.Join('\n', lines));
    }
}

public sealed record LoadedModInfo(
    string Name,
    string PackageName,
    string Version,
    string ResourcesMd5);
