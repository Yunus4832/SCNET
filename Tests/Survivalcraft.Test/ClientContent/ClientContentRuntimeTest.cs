using System.Xml.Linq;

using Game;
using Game.Application;
using Game.AssetReaders;
using Game.Blocks;
using Game.Blocks.Presentation;
using Game.ClientContent;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Content;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.ClientContent;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientContentRuntimeTest
{
    [Fact]
    public void DirectoryListingsRefreshAfterResourceChanges()
    {
        AssetManager.Initialize();
        var resource = new AssetInfo("Audio/Splashes/Test.ogg");
        var replacement = new AssetInfo(resource.AbsolutePath);
        var owner = ResourceId.Parse("example.sounds:splash").Namespace;
        try
        {
            Assert.Empty(AssetManager.List("Audio/Splashes"));
            AssetManager.AddOwned(owner, resource);
            Assert.Same(resource, Assert.Single(AssetManager.List("Audio/Splashes/")));
            AssetManager.AddOwned(owner, replacement);
            Assert.Same(replacement, Assert.Single(AssetManager.List("Audio/Splashes")));
            AssetManager.RemoveOwner(owner);
            Assert.Empty(AssetManager.List("Audio/Splashes"));
            AssetManager.AddOwned(owner, new AssetInfo(resource.AbsolutePath));
            Assert.Single(AssetManager.List("Audio/Splashes"));
            AssetManager.Initialize();
            Assert.Empty(AssetManager.List("Audio/Splashes"));
        }
        finally { AssetManager.Initialize(); }
    }

    [Fact]
    public void PreviewLifetimeDoesNotOwnWorldPresentationsOrCoreRules()
    {
        using var rules = CreateRules();
        var module = new TrackingModule();
        var runtime = new ClientContentRuntime([module]);
        runtime.InitializePreview(rules);
        var firstPreview = runtime.PreviewCatalog;
        var first = Assert.Single(module.Created);
        using var world = runtime.CreateCatalog(rules);
        var worldPresentation = module.Created[1];
        runtime.InitializePreview(rules);
        var replacement = module.Created[2];
        Assert.True(first.Disposed);
        Assert.NotSame(firstPreview, runtime.PreviewCatalog);
        Assert.False(worldPresentation.Disposed);
        Assert.Same(rules, world.Rules);
        runtime.Dispose();
        runtime.Dispose();
        Assert.True(replacement.Disposed);
        Assert.False(worldPresentation.Disposed);
        Assert.Equal(1, replacement.DisposeCount);
        Assert.False(((TrackingAirBlock)rules.GetBlock(TrackingModule.Air)).Disposed);
        Assert.Throws<ObjectDisposedException>(() => runtime.CreateCatalog(rules));
    }

    [Fact]
    public void FailedInitializationDisposesOnlyTheNewPresentationAndRetainsPreview()
    {
        using var rules = CreateRules();
        var module = new TrackingModule();
        using var runtime = new ClientContentRuntime([module]);
        runtime.InitializePreview(rules);
        var previous = runtime.PreviewCatalog;
        module.FailInitialize = true;
        Assert.Throws<InvalidOperationException>(() => runtime.InitializePreview(rules));
        Assert.Same(previous, runtime.PreviewCatalog);
        Assert.False(module.Created[0].Disposed);
        Assert.True(module.Created[1].Disposed);
    }

    [Fact]
    public void ExplicitModuleBindingsStayIsolatedAcrossRuleCatalogs()
    {
        using var firstRules = CreateRules();
        using var secondRules = CreateRules();
        using var runtime = new ClientContentRuntime([new TrackingModule()]);
        using var first = runtime.CreateCatalog(firstRules);
        using var second = runtime.CreateCatalog(secondRules);
        Assert.Same(firstRules.GetBlock(TrackingModule.Air), first.Get(TrackingModule.Air).Block);
        Assert.Same(secondRules.GetBlock(TrackingModule.Air), second.Get(TrackingModule.Air).Block);
        Assert.NotSame(first.Get(TrackingModule.Air), second.Get(TrackingModule.Air));
    }

    [Fact]
    public void DuplicateClientFactoriesFailBeforeResourceConstruction()
    {
        var module = new TrackingModule();
        Assert.Throws<InvalidOperationException>(() => new ClientContentRuntime([module, module]));
        Assert.Empty(module.Created);
    }

    [Fact]
    public void CacheDisposalFailureReleasesAllOldResourcesAndCommitsUsableReplacement()
    {
        Assert.Null(CurrentModRuntime.Value);
        ClientApplication.ReleaseContent();
        AssetManager.Initialize();
        var reader = new TrackingAssetReader();
        AssetManager.RegisterReader(reader);
        var resources = new[] { "example.one:first.bin", "example.one:second.bin", "example.two:third.bin" }
            .Select(ResourceId.Parse)
            .Select(id => new RegisteredContentResource(
                    id,
                    id.Path,
                    $"{id.Namespace}/{id.Path}",
                    () => new MemoryStream([1])
                )
            )
            .ToArray();
        using var rules = CreateRules();
        var module = new TrackingModule();
        var previous = new ClientContentRuntime([module]);
        previous.InstallResources(resources);
        previous.InitializePreview(rules);
        var oldResources = AssetManager.List().ToArray();
        var oldAssets = resources.Select(resource => AssetManager.Get<TrackingAsset>(resource.ContentPath[..^4]))
            .ToArray();
        var first = GameModRuntime.Start();
        var next = GameModRuntime.Start();
        var previousHttpEnabled = StartupManager.Current.Session.HttpCommandEnabled;
        StartupManager.Current.Session.HttpCommandEnabled = false;
        try
        {
            GameEntry.SetModRuntime(first);
            ClientApplication.InstallContent(previous);
            GameEntry.SetModRuntime(next);

            Assert.Same(next, CurrentModRuntime.Value);
            Assert.All(oldAssets, asset => Assert.Equal(1, asset.DisposeCount));
            Assert.All(oldResources, resource => Assert.True(resource.IsDisposed));
            Assert.All(
                resources,
                resource => Assert.False(AssetManager.ContainsOwned(resource.Id.Namespace, resource.ContentPath))
            );
            Assert.Equal(1, Assert.Single(module.Created).DisposeCount);
            previous.Dispose();
            Assert.All(oldAssets, asset => Assert.Equal(1, asset.DisposeCount));
            Assert.Throws<ObjectDisposedException>(first.InitializeContentData);
            next.InitializeContentData();

            var replacement = new ClientContentRuntime([]);
            replacement.InstallResources(resources);
            ClientApplication.InstallContent(replacement);
            var asset = AssetManager.Get<TrackingAsset>("example.one/first");
            Assert.DoesNotContain(asset, oldAssets);
            Assert.Equal(0, asset.DisposeCount);
        }
        finally
        {
            GameEntry.SetModRuntime(null);
            StartupManager.Current.Session.HttpCommandEnabled = previousHttpEnabled;
            AssetManager.Initialize();
        }
    }

    private sealed class TrackingAsset(bool failDispose) : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (failDispose) { throw new InvalidOperationException("Test cached asset disposal failure."); }
        }
    }

    private sealed class TrackingAssetReader : AssetReader
    {
        private bool _created;

        public override string Type => typeof(TrackingAsset).FullName!;

        public override string[] DefaultSuffix => ["bin"];

        public override object Get(AssetInfo[] contents)
        {
            var asset = new TrackingAsset(!_created);
            _created = true;
            return asset;
        }
    }

    private static BlockContentCatalog CreateRules()
    {
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(
            new ModId("game"),
            TrackingModule.Air,
            new BlockRegistration(static () => new TrackingAirBlock())
        );
        var catalog = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registry.Entries));
        catalog.Initialize(new Game.Blocks.Collision.CollisionGeometryCatalog([]), null, new XElement("Recipes"));
        return catalog;
    }

    private sealed class TrackingAirBlock : AirBlock
    {
        public bool Disposed { get; private set; }

        public override void Dispose() => Disposed = true;
    }

    private sealed class TrackingModule : IClientContentModule
    {
        public static ResourceId Air { get; } = ResourceId.Parse("game:air");

        public bool FailInitialize { get; set; }

        public List<TrackingPresentation> Created { get; } = [];

        public IEnumerable<BlockPresentationRegistration> BlockPresentations =>
        [
            new(
                Air,
                (block, catalog) =>
                {
                    var presentation = new TrackingPresentation((AirBlock)block, catalog, FailInitialize);
                    Created.Add(presentation);
                    return presentation;
                }
            )
        ];
    }

    private sealed class TrackingPresentation(AirBlock block, BlockPresentationCatalog catalog, bool failInitialize)
        : AirBlockPresentation(block, catalog)
    {
        public bool Disposed => DisposeCount > 0;

        public int DisposeCount { get; private set; }

        public override void Initialize()
        {
            if (failInitialize) { throw new InvalidOperationException("Test initialization failure."); }
        }

        public override void Dispose() => DisposeCount++;
    }
}
