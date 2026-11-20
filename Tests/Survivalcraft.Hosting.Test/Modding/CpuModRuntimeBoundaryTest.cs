using System.Runtime.Loader;

using Engine.Core.FileStorage;

using Game.Modding;
using Game.Modding.Blocks;
using Game.Protocol;
using Game.World;

namespace Survivalcraft.Hosting.Test.Modding;

public sealed class CpuModRuntimeBoundaryTest
{
    public CpuModRuntimeBoundaryTest()
    {
        Storage.RegisterFileSystemRoot("app", AppContext.BaseDirectory, readOnly: true);
    }

    [Fact]
    public void BuiltinRuntimeLoadsItsRealDatabaseAndCollisionCatalogWithoutApplicationAssemblies()
    {
        using var runtime = GameModRuntime.Start();
        runtime.InitializeContentData();
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var catalog = runtime.CreateWorldCatalog(world, BlockIndexMapping.Empty);
        try
        {
            Assert.Equal(ResourceId.Parse("game:air"), catalog.Registry.ById[ResourceId.Parse("game:air")].Id);
            Assert.NotNull(catalog.GetBlock(ResourceId.Parse("game:oak_leaves")));
            var collisionAsset = catalog.RegisteredBlocks.Select(block => block.CollisionModelAsset)
                .OfType<ResourceId>().First();
            Assert.NotEmpty(catalog.CollisionGeometry.Get(collisionAsset).Nodes);
            Assert.Contains(runtime.Content.RegisteredResources, resource => resource.RelativePath == "Database.xml");
            var language = runtime.Content.CreateLanguageCatalog("zh-CN");
            Assert.Equal("zh-CN", language.CurrentLanguage);
            Assert.False(string.IsNullOrEmpty(language.Ok));
            Assert.DoesNotContain(
                AssemblyLoadContext.Default.Assemblies,
                assembly => assembly.GetName().Name is "Survivalcraft" or "Survivalcraft.Client"
            );
            Assert.DoesNotContain(
                typeof(GameModRuntime).Assembly.GetReferencedAssemblies(),
                assembly => assembly.Name is "Survivalcraft" or "Survivalcraft.Client" or "Engine"
            );
        }
        finally { world.Close(); }
    }

    [Fact]
    public void MissingLocalProfilePackageCannotSilentlyLoadAnotherInstalledVersion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-missing-local-mod-" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = new ModProfile
            {
                Id = "test",
                Packages =
                [
                    new ModPackageRequirement
                        { ModId = "test.required", Version = "1.0.0", PackageHash = new string('a', 64) }
                ]
            };
            Assert.Throws<FileNotFoundException>(() => LocalModLoadPlan.Resolve(profile, directory));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void DependencyAndDescriptorEnumerationFailuresReleaseAcceptedLifetimesExactlyOnce()
    {
        var lifetime = new LifetimeCounter();
        var host = new ModHost();
        var descriptor = new ModDescriptor(
            new ModManifest("test.failure", "Failure", "1.0.0", [new ModDependency("test.missing")]),
            static () => new EmptyMod(),
            lifetime
        );
        Assert.Throws<ModDependencyException>(() => host.LoadAndStart([descriptor]));
        Assert.Equal(1, lifetime.Disposals);
        host.StopAll();
        Assert.Equal(1, lifetime.Disposals);
        var second = new LifetimeCounter();
        var next = new ModHost();
        Assert.Throws<InvalidDataException>(() => next.LoadAndStart(FailingEnumeration(second)));
        Assert.Equal(1, second.Disposals);
    }

    [Fact]
    public void ReplacementReservesWorldLifetimeBeforeClientResourcesCanBeReleased()
    {
        Assert.Null(CurrentModRuntime.Value);
        var mod = new StatefulMod();
        using var previous = GameModRuntime.Start(
            [new ModDescriptor(new ModManifest("test.state", "State", "1.0.0"), () => mod)]
        );
        using var next = GameModRuntime.Start();
        using (var install = CurrentModRuntime.BeginReplacement(previous)) { install.Commit(); }

        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        try
        {
            previous.InitializeContentData();
            previous.CreateWorldCatalog(world, BlockIndexMapping.Empty);
            Assert.Throws<InvalidOperationException>(() => CurrentModRuntime.BeginReplacement(next));
            Assert.Same(previous, CurrentModRuntime.Value);
            world.Close();
            var unboundWorld = new WorldRuntimeContext(RuntimeSide.Authority, false);
            try
            {
                using (CurrentModRuntime.BeginReplacement(next))
                {
                    Assert.Throws<InvalidOperationException>(() =>
                        previous.CreateWorldCatalog(unboundWorld, BlockIndexMapping.Empty)
                    );
                    Assert.Throws<InvalidOperationException>(() => mod.State!.Get(unboundWorld));
                    Assert.Throws<InvalidOperationException>(() => CurrentModRuntime.BeginReplacement(null));
                    Assert.Same(previous, CurrentModRuntime.Value);
                }

                // Cancellation restores the old owner and leaves the proposed replacement alive.
                Assert.Same(previous, CurrentModRuntime.Value);
                Assert.NotNull(mod.State!.Get(unboundWorld));
                Assert.Throws<InvalidOperationException>(() => CurrentModRuntime.BeginReplacement(next));
            }
            finally { unboundWorld.Close(); }

            var notifications = new List<GameModRuntime?>();
            void OnChanged(GameModRuntime? runtime) => notifications.Add(runtime);
            CurrentModRuntime.Changed += OnChanged;
            try
            {
                using var replacement = CurrentModRuntime.BeginReplacement(next);
                replacement.Commit();
                Assert.Same(next, CurrentModRuntime.Value);
                Assert.Equal(new[] { next }, notifications);
                Assert.Throws<ObjectDisposedException>(previous.InitializeDatabase);
                Assert.Throws<ObjectDisposedException>(replacement.Commit);
            }
            finally { CurrentModRuntime.Changed -= OnChanged; }
        }
        finally
        {
            world.Close();
            using var clear = CurrentModRuntime.BeginReplacement(null);
            clear.Commit();
        }
    }

    [Fact]
    public void DestructiveReplacementCompletesEveryCleanupAndTransfersOwnershipDespiteFailures()
    {
        Assert.Null(CurrentModRuntime.Value);
        var first = new LifetimeCounter();
        var failing = new FailingLifetime();
        var stop = new StopCounter();
        using var previous = GameModRuntime.Start(
            [
                new ModDescriptor(new ModManifest("test.cleanup.a", "First", "1.0.0"), () => stop, first),
                new ModDescriptor(
                    new ModManifest("test.cleanup.b", "Failure", "1.0.0"),
                    static () => new EmptyMod(),
                    failing
                )
            ]
        );
        using var next = GameModRuntime.Start();
        using (var install = CurrentModRuntime.BeginReplacement(previous)) { install.Commit(); }

        var notifications = 0;
        void FailingObserver(GameModRuntime? _) => throw new InvalidOperationException("Observer failed.");
        void FollowingObserver(GameModRuntime? _) => notifications++;
        CurrentModRuntime.Changed += FailingObserver;
        CurrentModRuntime.Changed += FollowingObserver;
        try
        {
            using var replacement = CurrentModRuntime.BeginReplacement(next);
            replacement.Commit();
            Assert.Same(next, CurrentModRuntime.Value);
            Assert.Equal(1, notifications);
            Assert.Equal(1, first.Disposals);
            Assert.Equal(1, failing.Disposals);
            Assert.Equal(1, stop.Stops);
            Assert.Throws<ObjectDisposedException>(previous.InitializeContentData);
            var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
            try
            {
                Assert.Throws<ObjectDisposedException>(() => previous.CreateWorldCatalog(world, BlockIndexMapping.Empty)
                );
                next.InitializeContentData();
                Assert.NotNull(next.CreateWorldCatalog(world, BlockIndexMapping.Empty));
            }
            finally { world.Close(); }

            previous.Dispose();
            Assert.Equal(1, first.Disposals);
            Assert.Equal(1, failing.Disposals);
        }
        finally
        {
            CurrentModRuntime.Changed -= FailingObserver;
            CurrentModRuntime.Changed -= FollowingObserver;
            using var clear = CurrentModRuntime.BeginReplacement(null);
            clear.Commit();
        }
    }

    private sealed class FailingLifetime : IDisposable
    {
        public int Disposals { get; private set; }

        public void Dispose()
        {
            Disposals++;
            throw new InvalidOperationException("Assembly cleanup failed.");
        }
    }

    private sealed class StopCounter : IMod
    {
        public int Stops { get; private set; }

        public void Configure(IModContext context)
        {
        }

        public void Start(IModContext context)
        {
        }

        public void Stop() => Stops++;
    }

    private sealed class StatefulMod : IMod
    {
        public ModWorldState<LifetimeCounter>? State { get; private set; }

        public void Configure(IModContext context)
        {
            State = context.Worlds.Register(
                ResourceId.Parse("test.state:value"),
                static _ => new LifetimeCounter()
            );
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private static IEnumerable<ModDescriptor> FailingEnumeration(LifetimeCounter lifetime)
    {
        yield return new ModDescriptor(
            new ModManifest("test.partial", "Partial", "1.0.0"),
            static () => new EmptyMod(),
            lifetime
        );
        throw new InvalidDataException("Descriptor enumeration failed.");
    }

    private sealed class LifetimeCounter : IDisposable
    {
        public int Disposals { get; private set; }

        public void Dispose() => Disposals++;
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
}
