using Game.Blocks;
using Game;
using Game.Modding;
using Game.Modding.Blocks;

using System.Xml.Linq;
using System.IO.Compression;
using System.Text;

using Engine.Core;

using Game.Managers;

namespace Survivalcraft.Test.Modding;

public class GameModRuntimeTest
{
    [Fact]
    public void RuntimeIncludesRequiredBuiltInContentMod()
    {
        var externalManifest = new ModManifest(
            "example.addon",
            "Addon",
            "1.0.0",
            [new ModDependency("game", "1.0.0")]);
        var external = new ModDescriptor(externalManifest, static () => new EmptyMod());

        using var runtime = GameModRuntime.Start([external]);

        Assert.Equal(["game", "example.addon"], runtime.Host.Runtimes.Select(item => item.Descriptor.Manifest.Id));
        Assert.True(runtime.Blocks.TryGet(new ResourceId(new ModId("game"), "air"), out var air));
        Assert.Equal(AirBlock.Index, air!.RuntimeIndex);
        Assert.Single(runtime.Blocks.DataEntries);
        Assert.Equal(["game", "example.addon"], runtime.GetLoadedMods().Select(item => item.PackageName));
    }

    [Fact]
    public void BlocksManagerConsumesCatalogDataWithoutLegacyModList()
    {
        var host = new ModHost();
        host.LoadAndStart([new ModDescriptor(
            new ModManifest("example", "Example", "1.0.0"),
            static () => new DataBlockMod())]);
        var blocks = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
        var data = host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName);
        var catalog = BlockRuntimeCatalog.Compile(blocks, data);

        BlocksManager.Initialize(catalog);

        Assert.Equal("Registry Air", BlocksManager.Blocks[0].DisplayName);
        Assert.True(BlocksManager.TryGetBlockId(0, out var id));
        Assert.Equal(new ResourceId(new ModId("example"), "air"), id);
        host.StopAll();
    }

    [Fact]
    public void CraftingRecipesManagerConsumesCompiledRecipeDocument()
    {
        var host = new ModHost();
        host.LoadAndStart([new ModDescriptor(
            new ModManifest("example", "Example", "1.0.0"),
            static () => new DataBlockMod())]);
        var blocks = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
        var blockData = host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName);
        BlocksManager.Initialize(BlockRuntimeCatalog.Compile(blocks, blockData));
        var recipes = new XElement(
            "Recipes",
            new XElement(
                "Recipe",
                new XAttribute("Result", "AirBlock"),
                new XAttribute("ResultCount", 1),
                new XAttribute("Description", string.Empty),
                new XAttribute("RequiredHeatLevel", 0),
                "\"\""));

        CraftingRecipesManager.Initialize(recipes);

        Assert.Single(CraftingRecipesManager.ReadonlyRecipes);
        host.StopAll();
    }

    [Fact]
    public void RuntimeInstallsBuiltInAssetsAndLanguage()
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();

        runtime.InitializeAssets();
        runtime.InitializeLanguage("zh-CN");

        Assert.True(ContentManager.ContainsKey("BlocksData.txt"));
        Assert.True(ContentManager.ContainsKey("Fonts/Pericles.lst"));
        Assert.Contains("zh-CN", runtime.Content.LanguageTypes);
        Assert.False(string.IsNullOrWhiteSpace(LanguageManager.Ok));
    }

    [Fact]
    public void RuntimeBuildsBuiltInContentDataWithoutLegacyModBootstrap()
    {
        Dispatcher.Initialize();
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();

        runtime.InitializeLanguage("zh-CN");
        runtime.InitializeContentData();

        Assert.NotNull(DatabaseManager.DatabaseNodeField);
        Assert.NotEmpty(CraftingRecipesManager.ReadonlyRecipes);
        Assert.True(BlocksManager.TryGetBlockId(0, out var id));
        Assert.Equal(new ResourceId(new ModId("game"), "air"), id);
    }

    [Fact]
    public void LegacyNoArgManagersPreferActiveGameRuntime()
    {
        Dispatcher.Initialize();
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start();
        GameEntry.SetModRuntime(runtime);
        try
        {
            runtime.InitializeLanguage("zh-CN");
            BlocksManager.Initialize();
            CraftingRecipesManager.Initialize();

            Assert.True(BlocksManager.TryGetBlockId(0, out var id));
            Assert.Equal(new ResourceId(new ModId("game"), "air"), id);
            Assert.NotEmpty(CraftingRecipesManager.ReadonlyRecipes);
        }
        finally
        {
            GameEntry.SetModRuntime(null);
        }
    }

    [Fact]
    public void BlocksManagerGetBlockPrefersActiveRuntimeCatalog()
    {
        using var runtime = GameModRuntime.Start();
        GameEntry.SetModRuntime(runtime);
        try
        {
            var block = BlocksManager.GetBlock("game", nameof(AirBlock));

            Assert.NotNull(block);
            Assert.Equal(0, block!.BlockIndex);
        }
        finally
        {
            GameEntry.SetModRuntime(null);
        }
    }

    [Fact]
    public void RuntimeApisWorkWithoutLegacyManifestAdoption()
    {
        using var runtime = GameModRuntime.Start();
        GameEntry.SetModRuntime(runtime);
        try
        {
            var loadedMods = runtime.GetLoadedMods();
            Assert.NotEmpty(loadedMods);
            Assert.Contains(loadedMods, item => item.PackageName == "game");
        }
        finally
        {
            GameEntry.SetModRuntime(null);
        }
    }

    [Fact]
    public void DirectoryRuntimeSkipsDisabledPackages()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-runtime-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WritePackage(Path.Combine(directory, "example.addon.scpak"), """
                {
                  "id": "example.addon",
                  "name": "Addon",
                  "version": "1.0.0"
                }
                """);
            ModSelectionSettings.ReplaceDisabledPackages(["example.addon"]);

            using var runtime = GameModRuntime.StartFromDirectory(directory, ModSide.Server);

            Assert.Equal(["game"], runtime.Host.Runtimes.Select(item => item.Descriptor.Manifest.Id));
        }
        finally
        {
            ModSelectionSettings.ReplaceDisabledPackages([]);
            Directory.Delete(directory, true);
        }
    }

    private sealed class EmptyMod : IMod
    {
        public void Configure(IModContext context)
        {
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class DataBlockMod : IMod
    {
        public void Configure(IModContext context)
        {
            context.Extensions.Register(
                BlockExtensions.RegistryName,
                new ResourceId(context.Manifest.ModId, "air"),
                new BlockRegistration(0, static () => new AirBlock()));
            context.Extensions.RegisterBlockData(
                new ResourceId(context.Manifest.ModId, "base"),
                static () => "Type;DisplayName\nAirBlock;Registry Air");
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private static void WritePackage(string path, string manifest)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var manifestEntry = archive.CreateEntry("manifest.json");
        using var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8, leaveOpen: false);
        writer.Write(manifest);
    }
}
