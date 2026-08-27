using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

using Engine.Core;

using Game;
using Game.Blocks;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Widgets;

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
        Assert.Equal(["game", "example.addon"], runtime.LoadedMods.Select(item => item.PackageName));
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
        var playerPanelTemplate = ContentManager.Get<XElement>("Widgets/PlayerPanelWidget");
        var messagePanelTemplate = ContentManager.Get<XElement>("Widgets/MessagePanelWidget");
        Assert.Equal("PlayerPanelWidget", playerPanelTemplate.Name.LocalName);
        Assert.Equal("MessagePanelWidget", messagePanelTemplate.Name.LocalName);

        var tabsTemplate = playerPanelTemplate.Elements()
            .Single(element => element.Attribute("Name")?.Value == "Tabs");
        new StackPanelWidget().LoadProperties(null, tabsTemplate);

        foreach (var hostTemplate in playerPanelTemplate.Elements()
                     .Where(element =>
                         element.Name.LocalName == nameof(CanvasWidget) &&
                         element.Attribute("Name") is not null))
        {
            new CanvasWidget().LoadProperties(null, hostTemplate);
        }

        var transcriptHostTemplate = messagePanelTemplate.Elements()
            .Single(element => element.Attribute("Name")?.Value == "TranscriptHost");
        new CanvasWidget().LoadProperties(null, transcriptHostTemplate);
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
            var loadedMods = runtime.LoadedMods;
            Assert.NotEmpty(loadedMods);
            Assert.Contains(loadedMods, item => item.PackageName == "game");
        }
        finally
        {
            GameEntry.SetModRuntime(null);
        }
    }

    [Fact]
    public void RuntimeCachesEffectiveProfileAndModDataHash()
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
                """, new Dictionary<string, string>
                {
                    ["data/blocks/items.csv"] = "Type;DisplayName\nAirBlock;Profile Runtime"
                });

            var descriptors = ModPackageCatalog.CreateLoadPlan(directory, ModSide.Server);
            using var runtime = GameModRuntime.Start(descriptors);

            var requirement = Assert.Single(runtime.EffectiveProfile.Packages);
            Assert.Equal("example.addon", requirement.ModId);
            Assert.Equal("1.0.0", requirement.Version);
            Assert.False(string.IsNullOrWhiteSpace(requirement.PackageHash));
            Assert.Equal(ModProfileManager.ComputeDataHash(runtime.EffectiveProfile), runtime.ModDataHash);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RuntimeUsesContentServerUrlWhenProfileDoesNotSpecifyRepository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-runtime-test-{Guid.NewGuid():N}");
        var previousRepositoryUrl = SettingsManager.Current.ContentServerUrl;
        Directory.CreateDirectory(directory);
        try
        {
            SettingsManager.Current.ContentServerUrl = "https://mods.example/";
            WritePackage(Path.Combine(directory, "example.addon.scpak"), """
                {
                  "id": "example.addon",
                  "name": "Addon",
                  "version": "1.0.0"
                }
                """, new Dictionary<string, string>
                {
                    ["data/blocks/items.csv"] = "Type;DisplayName\nAirBlock;Profile Runtime"
                });

            using var runtime = GameModRuntime.StartFromProfile(
                new ModProfile
                {
                    Id = "local",
                    Packages =
                    [
                        new ModPackageRequirement
                        {
                            ModId = "example.addon",
                            Version = "1.0.0"
                        }
                    ]
                },
                directory,
                ModSide.Server);

            Assert.Equal("https://mods.example", runtime.EffectiveProfile.RepositoryUrl);
            Assert.Equal("https://mods.example", runtime.CreateServerRequiredProfile()?.RepositoryUrl);
        }
        finally
        {
            SettingsManager.Current.ContentServerUrl = previousRepositoryUrl;
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

    private static void WritePackage(
        string path,
        string manifest,
        IReadOnlyDictionary<string, string>? dataFiles = null)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var manifestEntry = archive.CreateEntry("manifest.json");
        using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8, leaveOpen: false))
        {
            writer.Write(manifest);
        }

        if (dataFiles is null)
        {
            return;
        }

        foreach (var (entryPath, content) in dataFiles)
        {
            var entry = archive.CreateEntry(entryPath);
            using var dataWriter = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            dataWriter.Write(content);
        }
    }
}
