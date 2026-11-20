using EntitySystem.TemplatesDatabase;

using Game.Modding;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainChunkBehaviorNotifierTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyAuthorityNormalizationKeepsTheWorkerPausedWhileCallbacksCanAcquireAccess(bool replica)
    {
        using var fixture = new Fixture(replica: replica);
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        fixture.First.WhenGenerated = () =>
        {
            Assert.False(terrain.Access.IsHeldByCurrentThread);
            Assert.Equal(!replica, terrain.WorkExecutor.HasMainRequest);
            terrain.WorkExecutor.EnterMain();
            try { fixture.Chunk.SetCellValueFast(2, 60, 2, Terrain.MakeBlockValue(3)); }
            finally { terrain.WorkExecutor.LeaveMain(); }
        };

        Assert.True(fixture.Initialize());
        Assert.False(terrain.WorkExecutor.HasMainRequest);
        Assert.True(fixture.Chunk.IsContentInitialized);
        Assert.Equal(3, Terrain.ExtractContents(fixture.Chunk.GetCellValueFast(2, 60, 2)));
    }

    [Fact]
    public void InitializationRejectsACallersExistingAccessLeaseBeforeBusinessSideEffects()
    {
        using var fixture = new Fixture();
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        terrain.WorkExecutor.EnterMain();
        try
        {
            Assert.Throws<InvalidOperationException>(() => fixture.Initialize());
            Assert.True(terrain.Access.IsHeldByCurrentThread);
        }
        finally { terrain.WorkExecutor.LeaveMain(); }

        Assert.False(fixture.Chunk.AreBehaviorsNotified);
        Assert.False(fixture.World.Project.IsDisposed);
    }

    [Fact]
    public void AnUncaughtInitializationFailureClosesTheWorldWithoutRetryingPartialSideEffects()
    {
        using var fixture = new Fixture();
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        fixture.First.WhenInitialized = () => throw new InvalidOperationException("initialization failed");

        Assert.Throws<InvalidOperationException>(() => fixture.Initialize());

        Assert.True(fixture.World.Project.IsDisposed);
        Assert.False(terrain.WorkExecutor.HasMainRequest);
        Assert.False(fixture.Chunk.IsContentInitialized);
        Assert.False(fixture.Initialize());
        Assert.Equal(1, fixture.First.Initialized);
    }

    [Fact]
    public void ReentrantAndPerCellNormalizationCannotPublishAHalfInitializedBaseline()
    {
        using var fixture = new Fixture(TerrainContentOrigin.Generated);
        var authority = new Game.Terrains.Distribution.TerrainChunkContentAuthority(fixture.Terrain);
        fixture.First.WhenInitialized = () =>
        {
            Assert.True(fixture.Chunk.AreBehaviorsNotified);
            Assert.False(fixture.Chunk.IsContentInitialized);
            Assert.False(authority.TryGetDescriptor(fixture.Chunk.Coords, out _));
        };
        fixture.First.WhenGenerated = () =>
        {
            Assert.False(fixture.Chunk.IsContentInitialized);
            Assert.False(authority.TryGetSnapshot(fixture.Chunk.Coords, out _));
            fixture.Chunk.SetCellValueFast(2, 40, 2, Terrain.MakeBlockValue(3, 11));
        };
        Assert.True(fixture.Initialize());
        Assert.True(fixture.Chunk.IsContentInitialized);
        Assert.True(authority.TryGetSnapshot(fixture.Chunk.Coords, out var baseline));
        Assert.Equal(Terrain.MakeBlockValue(3, 11), baseline.Cells.Span[TerrainChunk.CalculateCellIndex(2, 40, 2)]);
        Assert.Equal(fixture.Chunk.NetworkContentRevision + 1, baseline.ContentVersion);
    }

    [Theory]
    [InlineData(TerrainContentOrigin.Generated, false)]
    [InlineData(TerrainContentOrigin.Storage, true)]
    [InlineData(TerrainContentOrigin.Replica, true)]
    public void ActualGeneratedCallbacksReceiveTheSourceAndAreNotRepeatedAfterRelighting(
        TerrainContentOrigin origin,
        bool loaded
    )
    {
        using var fixture = new Fixture(origin);
        Assert.True(fixture.Initialize());
        Assert.Equal(1, fixture.First.Initialized);
        Assert.Equal(1, fixture.Second.Initialized);
        Assert.Equal(1, fixture.InitializedHooks);
        foreach (var behavior in new[] { fixture.First, fixture.Second })
        {
            Assert.Equal(2, behavior.Generated.Count);
            Assert.All(behavior.Generated, call => Assert.Equal(loaded, call.Loaded));
            Assert.Equal(fixture.Chunk.GetCellValueFast(1, 60, 2), behavior.Generated[0].Value);
            Assert.Equal((fixture.Chunk.Origin.X + 1, 60, fixture.Chunk.Origin.Y + 2), behavior.Generated[0].Position);
            Assert.Equal((fixture.Chunk.Origin.X + 1, 61, fixture.Chunk.Origin.Y + 2), behavior.Generated[1].Position);
        }

        Assert.False(fixture.Initialize());
        fixture.Chunk.DerivedRevisions.InvalidateLight();
        Assert.False(fixture.Initialize());
        fixture.MakeReady();
        Assert.False(fixture.Initialize());
        Assert.Equal(2, fixture.First.Generated.Count);
        Assert.Equal(1, fixture.InitializedHooks);
    }

    [Fact]
    public void RetainedReplicaLeaseDoesNotReplayBusinessInitialization()
    {
        using var fixture = new Fixture(TerrainContentOrigin.Replica);
        Assert.True(fixture.Initialize());
        fixture.Terrain.RenewChunkAllocation(fixture.Chunk);
        fixture.MakeReady();
        Assert.False(fixture.Initialize());
        Assert.Equal(2, fixture.First.Generated.Count);
        Assert.Equal(1, fixture.InitializedHooks);
    }

    [Fact]
    public void InitializationWaitsForContentAndLightAndRejectsUnspecifiedPublishedOrigin()
    {
        using var fixture = new Fixture(TerrainContentOrigin.Unspecified, ready: false);
        Assert.False(fixture.Initialize());
        Assert.False(fixture.Chunk.AreBehaviorsNotified);
        fixture.MakeReady();
        fixture.Chunk.HasContents = false;
        Assert.False(fixture.Initialize());
        fixture.Chunk.HasContents = true;
        Assert.Throws<InvalidOperationException>(() => fixture.Initialize());
        Assert.False(fixture.Chunk.AreBehaviorsNotified);
        Assert.Empty(fixture.First.Generated);
    }

    [Fact]
    public void InitializationIsMarkedBeforeAnExternalCallbackCanReenter()
    {
        using var fixture = new Fixture();
        var events = 0;
        Assert.True(
            fixture.Initialize(chunk =>
                {
                    events++;
                    Assert.True(chunk.AreBehaviorsNotified);
                    Assert.False(fixture.Initialize());
                }
            )
        );
        Assert.Equal(1, events);
        Assert.Equal(1, fixture.First.Initialized);
        Assert.Equal(2, fixture.First.Generated.Count);
    }

    [Fact]
    public void ClosingFromTheFirstExternalHandlerStopsTheRestOfTheMulticast()
    {
        using var fixture = new Fixture();
        var laterHandler = false;
        Action<TerrainChunk> handlers = _ => fixture.World.Project.Dispose();
        handlers += _ => laterHandler = true;

        Assert.False(fixture.Initialize(handlers));
        Assert.False(laterHandler);
        Assert.True(fixture.Chunk.AreBehaviorsNotified);
        Assert.False(fixture.Chunk.IsContentInitialized);
        Assert.Equal(0, fixture.First.Initialized);
        Assert.Empty(fixture.First.Generated);
        Assert.Equal(0, fixture.InitializedHooks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingFromABehaviorStopsLaterBehaviorsCellsAndWorldHooks(bool duringChunkInitialization)
    {
        using var fixture = new Fixture();
        if (duringChunkInitialization) { fixture.First.WhenInitialized = fixture.World.Project.Dispose; }
        else { fixture.First.WhenGenerated = fixture.World.Project.Dispose; }

        Assert.False(fixture.Initialize());
        Assert.Equal(1, fixture.First.Initialized);
        Assert.Equal(duringChunkInitialization ? 0 : 1, fixture.Second.Initialized);
        Assert.Equal(duringChunkInitialization ? 0 : 1, fixture.First.Generated.Count);
        Assert.Empty(fixture.Second.Generated);
        Assert.Equal(0, fixture.InitializedHooks);
    }

    [Fact]
    public void DiscardNotificationUsesTheRealBehaviorTableAndDoesNotFreeContents()
    {
        using var fixture = new Fixture();
        var events = 0;
        Assert.True(fixture.Notifier.NotifyDiscarding(fixture.Chunk, _ => events++));
        Assert.Equal(1, events);
        Assert.Equal(1, fixture.First.Discarded);
        Assert.Equal(1, fixture.Second.Discarded);
        Assert.Equal(1, fixture.DiscardingHooks);
        Assert.Same(fixture.Chunk, fixture.Terrain.GetChunkAtCoords(2, -3));
        Assert.False(fixture.Chunk.IsDisposed);
        Assert.True(fixture.Chunk.HasContents);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("behavior")]
    [InlineData("hook")]
    public void DiscardStopsImmediatelyWhenAnExternalCallbackClosesTheWorld(string stage)
    {
        using var fixture = new Fixture();
        var laterHandler = false;
        Action<TerrainChunk>? handlers = null;
        if (stage == "event")
        {
            handlers = _ => fixture.World.Project.Dispose();
            handlers += _ => laterHandler = true;
        }
        else if (stage == "behavior") { fixture.First.WhenDiscarded = fixture.World.Project.Dispose; }
        else { fixture.WhenDiscardingHook = fixture.World.Project.Dispose; }

        Assert.False(fixture.Notifier.NotifyDiscarding(fixture.Chunk, handlers));
        Assert.False(laterHandler);
        Assert.Equal(stage == "event" ? 0 : 1, fixture.First.Discarded);
        Assert.Equal(stage == "hook" ? 1 : 0, fixture.Second.Discarded);
        Assert.Equal(stage == "hook" ? 1 : 0, fixture.DiscardingHooks);
    }

    [Fact]
    public void DiscardRejectsCallerAccessWithoutReleasingItOrClosingTheWorld()
    {
        using var fixture = new Fixture();
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        terrain.WorkExecutor.EnterMain();
        try
        {
            Assert.Throws<InvalidOperationException>(() => fixture.Notifier.NotifyDiscarding(fixture.Chunk));
            Assert.True(terrain.Access.IsHeldByCurrentThread);
            Assert.False(fixture.World.Project.IsDisposed);
            Assert.Equal(0, fixture.First.Discarded);
        }
        finally { terrain.WorkExecutor.LeaveMain(); }

        Assert.True(fixture.Notifier.NotifyDiscarding(fixture.Chunk));
    }

    [Fact]
    public void UncaughtDiscardFailureClosesTheWorldWithoutRunningLaterCallbacks()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("Discard failed.");
        fixture.First.WhenDiscarded = () => throw failure;
        Assert.Same(
            failure,
            Assert.Throws<InvalidOperationException>(() => fixture.Notifier.NotifyDiscarding(fixture.Chunk))
        );
        Assert.True(fixture.World.Project.IsDisposed);
        Assert.Equal(1, fixture.First.Discarded);
        Assert.Equal(0, fixture.Second.Discarded);
        Assert.False(fixture.Notifier.NotifyDiscarding(fixture.Chunk));
    }

    [Fact]
    public void InitializationResumesBetweenHandlersWithoutPublishingPartialContents()
    {
        using var fixture = new Fixture();
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        var original = fixture.Chunk.GetCellValueFast(1, 60, 2);
        fixture.First.WhenGenerated = () =>
        {
            fixture.Chunk.SetCellValueFast(1, 60, 2, Terrain.MakeBlockValue(3, 9));
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(5));
        };

        Assert.False(fixture.Initialize());
        Assert.Same(fixture.Chunk, fixture.Notifier.PendingChunk);
        Assert.True(fixture.Chunk.AreBehaviorsNotified);
        Assert.False(fixture.Chunk.IsContentInitialized);
        Assert.False(terrain.WorkExecutor.HasMainRequest);
        Assert.Single(fixture.First.Generated);
        Assert.Empty(fixture.Second.Generated);
        Assert.Equal(0, fixture.InitializedHooks);
        Assert.False(new TerrainChunkContentAuthority(fixture.Terrain).TryGetSnapshot(fixture.Chunk.Coords, out _));

        fixture.First.WhenGenerated = null;
        Assert.True(fixture.Initialize());
        Assert.Null(fixture.Notifier.PendingChunk);
        Assert.Equal(original, fixture.Second.Generated[0].Value);
        Assert.Equal(2, fixture.First.Generated.Count);
        Assert.Equal(2, fixture.Second.Generated.Count);
        Assert.Equal(1, fixture.First.Initialized);
        Assert.Equal(1, fixture.Second.Initialized);
        Assert.Equal(1, fixture.InitializedHooks);
        Assert.True(fixture.Chunk.IsContentInitialized);
    }

    [Fact]
    public void SnapshotPreparationCompletesTheActiveCursorOnce()
    {
        using var fixture = new Fixture();
        fixture.First.WhenInitialized = () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.False(fixture.Initialize());
        Assert.Equal(0, fixture.Second.Initialized);
        Assert.Empty(fixture.First.Generated);
        fixture.Notifier.CompletePendingInitialization();
        fixture.Notifier.CompletePendingInitialization();
        Assert.Null(fixture.Notifier.PendingChunk);
        Assert.Equal(1, fixture.First.Initialized);
        Assert.Equal(1, fixture.Second.Initialized);
        Assert.Equal(2, fixture.Second.Generated.Count);
        Assert.Equal(1, fixture.InitializedHooks);
        Assert.True(fixture.Chunk.IsContentInitialized);
    }

    [Fact]
    public void SnapshotPreparationRejectsReentrancyInsideCallbacks()
    {
        using var fixture = new Fixture();
        fixture.First.WhenInitialized = () =>
            Assert.Throws<InvalidOperationException>(fixture.Notifier.CompletePendingInitialization);
        Assert.True(fixture.Initialize());
        Assert.False(fixture.World.Project.IsDisposed);
    }

    [Fact]
    public void DisposedChunkCancelsTheCursorWithoutRunningRemainingCallbacks()
    {
        using var fixture = new Fixture();
        fixture.First.WhenInitialized = () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.False(fixture.Initialize());
        fixture.Terrain.FreeChunk(fixture.Chunk);
        Assert.False(fixture.Initialize());
        Assert.Null(fixture.Notifier.PendingChunk);
        Assert.Equal(0, fixture.Second.Initialized);
        Assert.Empty(fixture.Second.Generated);
        Assert.Equal(0, fixture.InitializedHooks);
    }

    [Fact]
    public void FailureAfterYieldClosesTheWorldAndReleasesTheWorkerPause()
    {
        using var fixture = new Fixture();
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        fixture.First.WhenGenerated = () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.False(fixture.Initialize());
        fixture.Second.WhenGenerated = () => throw new InvalidOperationException("resumed failure");
        Assert.Throws<InvalidOperationException>(() => fixture.Initialize());
        Assert.True(fixture.World.Project.IsDisposed);
        Assert.Null(fixture.Notifier.PendingChunk);
        Assert.False(terrain.WorkExecutor.HasMainRequest);
    }

    [Fact]
    public void EmptyBudgetDoesNotBeginCallbacks()
    {
        using var fixture = new Fixture();
        Assert.False(fixture.Initialize(maximumTime: TimeSpan.Zero));
        Assert.False(fixture.Chunk.AreBehaviorsNotified);
        Assert.Null(fixture.Notifier.PendingChunk);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Initialize(maximumTime: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void BackgroundDerivationWaitsUntilNormalizationFinishes()
    {
        using var fixture = new Fixture();
        var terrain = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        using var scheduler = new TerrainComputationScheduler(
            terrain,
            new TerrainLightingCalculator(
                fixture.Terrain,
                fixture.World.Catalog,
                false,
                static chunk => chunk.HasContents
            )
        );
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Computations))!.SetValue(terrain, scheduler);
        fixture.First.WhenInitialized = () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.False(fixture.Initialize());
        fixture.Chunk.DerivedRevisions.InvalidateHeightsAndLight();
        scheduler.UpdateDemand(
            [new TerrainUpdateLocation { Center = fixture.Chunk.Center, VisibilityDistance = 64, ContentDistance = 64 }]
        );
        Assert.True(scheduler.Step());
        Assert.False(fixture.Chunk.HasCurrentHeightData);
        Assert.True(scheduler.Counts.Total > 0);
        fixture.Notifier.CompletePendingInitialization();
        Assert.False(scheduler.Step());
        Assert.True(fixture.Chunk.HasCurrentHeightData);
    }

    [Fact]
    public void RetainedAllocationResumesTheSameCallbacksAfterYield()
    {
        using var fixture = new Fixture(TerrainContentOrigin.Replica, replica: true);
        fixture.First.WhenGenerated = () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.False(fixture.Initialize());
        fixture.Terrain.RenewChunkAllocation(fixture.Chunk);
        fixture.First.WhenGenerated = null;
        Assert.True(fixture.Initialize());
        Assert.Equal(1, fixture.First.Initialized);
        Assert.Equal(2, fixture.First.Generated.Count);
        Assert.Equal(2, fixture.Second.Generated.Count);
        Assert.Equal(1, fixture.InitializedHooks);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    private sealed class Fixture : IDisposable
    {
        public WorldCatalogScope World { get; }

        public Terrain Terrain { get; } = new();

        public RecordingBehavior First { get; }

        public RecordingBehavior Second { get; }

        public TerrainChunk Chunk { get; }

        public TerrainChunkBehaviorNotifier Notifier { get; }

        public ManualClock Clock { get; } = new();

        public bool Initialize(Action<TerrainChunk>? callback = null, TimeSpan? maximumTime = null)
        {
            return Notifier.TryInitialize(Chunk, maximumTime ?? TimeSpan.FromMilliseconds(4), callback);
        }

        public int InitializedHooks { get; private set; }

        public int DiscardingHooks { get; private set; }

        public Action? WhenDiscardingHook { get; set; }

        public Fixture(
            TerrainContentOrigin origin = TerrainContentOrigin.Generated,
            bool ready = true,
            bool replica = false
        )
        {
            World = new WorldCatalogScope(replica ? RuntimeSide.Client : RuntimeSide.Authority);
            var terrain = new SubsystemTerrain { Terrain = Terrain };
            World.Project.AddSubsystem(terrain);
            if (replica)
            {
                typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.ClientChunkContents))!
                    .SetValue(terrain, new ClientChunkContentCoordinator(Terrain));
            }

            Assert.True(World.Catalog.TryGetBlockId(3, out var id));
            First = new FirstRecordingBehavior(id);
            Second = new SecondRecordingBehavior(id);
            World.Project.AddSubsystem(First);
            World.Project.AddSubsystem(Second);
            First.Load(new ValuesDictionary());
            Second.Load(new ValuesDictionary());
            var behaviors = new SubsystemBlockBehaviors();
            World.Project.AddSubsystem(behaviors);
            behaviors.Load(new ValuesDictionary());
            Notifier = new TerrainChunkBehaviorNotifier(terrain, behaviors, Clock);
            typeof(SubsystemTerrain).GetField(
                    "_notifications",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .SetValue(terrain, Notifier);
            Chunk = Terrain.AllocateChunk(2, -3);
            Chunk.ContentOrigin = origin;
            Chunk.HasContents = true;
            Chunk.SetCellValueFast(1, 60, 2, Terrain.MakeBlockValue(3, 4));
            Chunk.SetCellValueFast(1, 61, 2, Terrain.MakeBlockValue(3, 2));
            if (ready) { MakeReady(); }

            WorldRuleHooks.For(World.Project).Dispose();
            var gameplay = new GameplayHooks();
            var owned = gameplay.ForOwner(new ModId("test.chunk-notify"));
            owned.OnTerrainChunkInitialized(_ => InitializedHooks++);
            owned.OnTerrainChunkDiscarding(_ =>
                {
                    DiscardingHooks++;
                    WhenDiscardingHook?.Invoke();
                }
            );
            gameplay.Freeze();
            var blockHooks = new BlockBehaviorHooks();
            blockHooks.Freeze();
            World.Project.BindRuleHooks(gameplay, blockHooks);
        }

        public void MakeReady()
        {
            var request = Chunk.DerivedRevisions.Capture();
            Assert.True(Chunk.DerivedRevisions.PublishHeights(request));
            Chunk.DerivedRevisions.PublishLightSeed(request);
            Assert.True(Chunk.DerivedRevisions.PublishLight(request));
        }

        public void Dispose()
        {
            try { World.Dispose(); }
            finally { Terrain.Dispose(); }
        }
    }

    private sealed class FirstRecordingBehavior(ResourceId id) : RecordingBehavior(id)
    {
    }

    private sealed class SecondRecordingBehavior(ResourceId id) : RecordingBehavior(id)
    {
    }

    private class RecordingBehavior(ResourceId id) : SubsystemBlockBehavior
    {
        public override ResourceId[] HandledBlocks => [id];

        public int Initialized { get; private set; }

        public int Discarded { get; private set; }

        public List<(int Value, (int X, int Y, int Z) Position, bool Loaded)> Generated { get; } = [];

        public Action? WhenInitialized { get; set; }

        public Action? WhenGenerated { get; set; }

        public Action? WhenDiscarded { get; set; }

        public override void OnChunkInitialized(TerrainChunk chunk)
        {
            Initialized++;
            WhenInitialized?.Invoke();
        }

        public override void OnChunkDiscarding(TerrainChunk chunk)
        {
            Discarded++;
            WhenDiscarded?.Invoke();
        }

        public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
        {
            Generated.Add((value, (x, y, z), isLoaded));
            WhenGenerated?.Invoke();
        }
    }
}
