using System.Xml.Linq;

using Engine.Core;

using Game;
using Game.Blocks;
using Game.Content;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Terrains;
using Game.Widgets;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
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
        Assert.True(runtime.Host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName).TryGet(new ResourceId(new ModId("game"), "air"), out var air));
        Assert.IsType<AirBlock>(air!.Factory());
        Assert.Single(runtime.Host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName).Entries);
        Assert.Equal(["game", "example.addon"], runtime.LoadedMods.Select(item => item.PackageName));
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
    public void HeadlessBuildsAndReleasesWorldContentWithoutPreview()
    {
        var previousMode = RunMode.Value;
        RunMode.Value = RunModeType.HeadlessServer;
        try
        {
            Dispatcher.Initialize();
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start();
            runtime.InitializeLanguage("zh-CN");
            runtime.InitializeContentData();
            Assert.NotNull(DatabaseManager.DatabaseNodeField);
            Assert.Throws<InvalidOperationException>(() => runtime.PreviewCatalog);
            var world = runtime.CreateWorldCatalog(BlockIndexMapping.Empty);
            Assert.NotEmpty(world.Recipes.ReadonlyRecipes);
            Assert.True(world.TryGetBlockId(0, out var id));
            Assert.Equal(ResourceId.Parse("game:air"), id);
            runtime.ReleaseWorldCatalog();
            Assert.Null(runtime.WorldCatalog);
        }
        finally
        {
            RunMode.Value = previousMode;
        }
    }

    [Fact]
    public void BuiltInFactoriesRegisteredAsModBlocksKeepTheirOwnCreativeIndices()
    {
        var previousMode = RunMode.Value;
        RunMode.Value = RunModeType.HeadlessServer;
        try
        {
            Dispatcher.Initialize();
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start([
                new ModDescriptor(new ModManifest("clone", "Cloned blocks", "1.0.0"), static () => new CloneBlocksMod())
            ]);
            runtime.InitializeLanguage("zh-CN");
            runtime.InitializeContentData();
            var world = runtime.CreateWorldCatalog(BlockIndexMapping.Empty);
            var count = 0;
            foreach (var entry in world.Registry.ById.Values.Where(entry => entry.Id.Namespace == new ModId("clone")))
            {
                foreach (var value in entry.Block.GetCreativeValues())
                {
                    Assert.True(entry.RuntimeIndex == Terrain.ExtractContents(value),
                        $"{entry.Id} emitted {Terrain.ExtractContents(value)} instead of {entry.RuntimeIndex}.");
                    count++;
                }

                foreach (var recipe in entry.Block.GetProceduralCraftingRecipes())
                {
                    Assert.True(recipe.ResultValue == 0 || entry.RuntimeIndex == Terrain.ExtractContents(recipe.ResultValue),
                        $"{entry.Id} recipe emitted {Terrain.ExtractContents(recipe.ResultValue)} instead of {entry.RuntimeIndex}.");
                }
            }

            Assert.True(count > 1000);
        }
        finally
        {
            RunMode.Value = previousMode;
        }
    }

    [Fact]
    public void FailedWorldInitializationDisposesBlocksWithoutPublishingCatalog()
    {
        var previousMode = RunMode.Value;
        RunMode.Value = RunModeType.HeadlessServer;
        var mod = new BrokenBlockMod();
        try
        {
            Dispatcher.Initialize();
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start([
                new ModDescriptor(new ModManifest("test", "Broken", "1.0.0"), () => mod)
            ]);
            runtime.InitializeLanguage("zh-CN");
            runtime.InitializeContentData();
            var exception = Assert.Throws<InvalidOperationException>(() =>
                runtime.CreateWorldCatalog(BlockIndexMapping.Empty));
            Assert.Equal("Intentional block initialization failure.", exception.Message);
            Assert.Null(runtime.WorldCatalog);
            Assert.Equal(1, mod.Block!.DisposeCount);
        }
        finally
        {
            RunMode.Value = previousMode;
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
            ImportPackage(directory, """
                                     {
                                       "id": "example.addon",
                                       "name": "Addon",
                                       "version": "1.0.0"
                                     }
                                     """, new Dictionary<string, string>
            {
                ["data/blocks/items.csv"] = "Id;DisplayName\ngame:air;Profile Runtime"
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
    public void RuntimeRequiredProfilePreservesExactHashWithoutRepositoryBinding()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-runtime-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var package = ImportPackage(directory, """
                                     {
                                       "id": "example.addon",
                                       "name": "Addon",
                                       "version": "1.0.0"
                                     }
                                     """, new Dictionary<string, string>
            {
                ["data/blocks/items.csv"] = "Id;DisplayName\ngame:air;Profile Runtime"
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
                            Version = "1.0.0",
                            PackageHash = package.PackageHash
                        }
                    ]
                },
                directory,
                ModSide.Server);

            var requirement = Assert.Single(runtime.CreateServerRequiredProfile()!.Packages);
            Assert.Equal(package.PackageHash, requirement.PackageHash);
        }
        finally
        {
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

    private sealed class CloneBlocksMod : IMod
    {
        public void Configure(IModContext context)
        {
            foreach (var entry in BuiltInBlockRegistry.Entries)
            {
                context.Extensions.Register(BlockExtensions.RegistryName,
                    new ResourceId(context.Manifest.ModId, entry.Path), new BlockRegistration(entry.Factory));
            }

            context.Extensions.RegisterBlockData(ResourceId.Parse("clone:base"), static () =>
                string.Join('\n', ContentManager.Get<string>("BlocksData").Split('\n').Select(line =>
                    line.StartsWith("game:", StringComparison.Ordinal) ? "clone:" + line["game:".Length..] : line)));
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class BrokenBlockMod : IMod
    {
        public BrokenBlock? Block { get; private set; }

        public void Configure(IModContext context)
        {
            context.Extensions.Register(BlockExtensions.RegistryName, ResourceId.Parse("test:broken"),
                new BlockRegistration(() => Block = new BrokenBlock()));
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class BrokenBlock : AirBlock
    {
        public int DisposeCount { get; private set; }

        public override void Initialize() => throw new InvalidOperationException("Intentional block initialization failure.");

        public override void Dispose() => DisposeCount++;
    }

    private static ContentPackageCacheEntry ImportPackage(
        string directory,
        string manifest,
        IReadOnlyDictionary<string, string>? dataFiles = null)
    {
        using var package = ScpkgTestPackage.Create(manifest, dataFiles);
        var cache = new ContentPackageCache(directory);
        return cache.ImportAsync(package).GetAwaiter().GetResult();
    }
}
