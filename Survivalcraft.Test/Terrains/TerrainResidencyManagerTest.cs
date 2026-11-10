using System.Collections.Concurrent;
using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Handling;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Rendering;
using Game.TerrainSerializers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainResidencyManagerTest
{
    [Fact]
    public void FullSaveCapacityDefersWithoutDiscardCallbacksOrNewAllocations()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = new ConcurrentQueue<int>();
        using var fixture = new Fixture(
            writer: (_, cells, _) =>
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException(); }

                writes.Enqueue(cells[0]);
            }
        );
        try
        {
            // Isolate the allocation whose unload transaction is under test.
            fixture.Terrain.AllocateChunk(0, 0);
            var source = fixture.Terrain.GetChunkAtCoords(0, 0)!;
            source.HasContents = true;
            source.ModificationCounter = 1;
            source.Cells[0] = 11;
            using var pending = new TerrainChunk(fixture.Terrain, 100, 100)
            {
                HasContents = true, ModificationCounter = 1
            };
            pending.Cells[0] = 1;
            Assert.True(fixture.Saves!.TryQueueChunkForUnload(pending));
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));

            var deferred = fixture.Update([Location(10)]);
            Assert.False(deferred.Changed);
            Assert.True(deferred.RetryRequired);
            Assert.Equal(1, fixture.Manager.DeferredUpdates);
            Assert.Equal(0, fixture.Discarded);
            Assert.Same(source, fixture.Terrain.GetChunkAtCoords(0, 0));
            Assert.Null(fixture.Terrain.GetChunkAtCoords(10, 0));
            Assert.Equal(1, source.ModificationCounter);

            release.Set();
            fixture.Saves.Flush();
            var completed = fixture.Update([Location(10)]);
            fixture.Saves.Flush();
            Assert.True(completed.Changed);
            Assert.False(completed.RetryRequired);
            Assert.Equal(1, fixture.Discarded);
            Assert.True(source.IsDisposed);
            Assert.NotNull(fixture.Terrain.GetChunkAtCoords(10, 0));
            Assert.Equal(new[] { 1, 11 }, writes.ToArray());
        }
        finally { release.Set(); }
    }

    [Fact]
    public void ADiscardCallbackCanDirtyACleanChunkWithoutLosingItsReservedSaveSlot()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        var source = fixture.Terrain.GetChunkAtCoords(0, 0)!;
        source.HasContents = true;
        source.Cells[0] = 5;
        using var nested = new TerrainChunk(fixture.Terrain, 100, 100)
        {
            HasContents = true, ModificationCounter = 1
        };
        nested.Cells[0] = 1;
        var nestedAccepted = true;
        fixture.OnDiscarding = chunk =>
        {
            chunk.Cells[0] = 99;
            chunk.ModificationCounter++;
            nestedAccepted = fixture.Saves!.TryQueueChunkForUnload(nested);
        };

        var result = fixture.Update([Location(10)]);
        fixture.Saves!.Flush();

        Assert.True(result.Changed);
        Assert.False(result.RetryRequired);
        Assert.False(nestedAccepted);
        Assert.Equal(1, nested.ModificationCounter);
        Assert.Equal(1, fixture.Discarded);
        Assert.True(source.IsDisposed);
        Assert.Equal(new[] { 99 }, fixture.Writes.ToArray());
    }

    [Fact]
    public void RetainedReplicaKeepsItsCellsAndUploadedGeometryButRenewsValidationIdentity()
    {
        using var fixture = new Fixture(replica: true);
        using var presentations = new TerrainChunkPresentationStore();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        var source = fixture.Terrain.GetChunkAtCoords(0, 0)!;
        source.HasContents = true;
        source.ContentOrigin = TerrainContentOrigin.Replica;
        source.NetworkContentVersion = 10;
        source.Cells[0] = 11;
        var request = source.DerivedRevisions.Capture();
        Assert.True(source.DerivedRevisions.PublishHeights(request));
        Assert.True(source.DerivedRevisions.PublishSunlight(request));
        Assert.True(source.DerivedRevisions.PublishLight(request));
        var presentation = presentations.Get(source);
        TerrainMeshTestData.Complete(source, presentation);
        presentation.MarkUploaded();
        var previous = new ChunkAllocationId(source.Coords, source.AllocationGeneration);
        fixture.Transport!.Request([new ChunkContentRequest(previous, source.NetworkContentVersion)]);
        var discarded = new List<ChunkAllocationId>();
        fixture.Transport.AllocationDiscarded += allocation =>
        {
            if (allocation.Coords == source.Coords) { discarded.Add(allocation); }
        };
        var renewed = new List<ulong>();
        fixture.Manager.ChunkAllocationRenewed += (chunk, generation) =>
        {
            if (ReferenceEquals(chunk, source)) { renewed.Add(generation); }

            presentations.RenewAllocation(chunk, generation);
        };

        fixture.Update([Location(2)]);
        Assert.Same(source, fixture.Terrain.GetChunkAtCoords(0, 0));
        Assert.Equal(0, fixture.Discarded);
        fixture.Update([Location(0)]);

        Assert.Equal(new[] { previous.Generation }, renewed);
        Assert.Equal(new[] { previous }, discarded);
        Assert.True(source.AllocationGeneration > previous.Generation);
        Assert.True(source.HasContents);
        Assert.Equal(11, source.Cells[0]);
        Assert.Equal(10, source.NetworkContentVersion);
        Assert.True(source.NeedsContentSnapshot);
        Assert.True(source.NeedsContentValidation);
        Assert.False(fixture.Transport.IsRequested(previous));
        Assert.True(presentation.GeometryUploaded);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, source, presentation));
        Assert.False(presentation.HasCurrentUploadedGeometry(source));
        Assert.False(fixture.Contents!.TryInstall(Snapshot(source, previous)));
        var current = new ChunkAllocationId(source.Coords, source.AllocationGeneration);
        Assert.True(fixture.Contents.TryInstall(Snapshot(source, current)));
        Assert.False(source.NeedsContentValidation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeavingTheRetentionMarginDisposesTheActualAllocationAndDropsItsRequest(bool hasContents)
    {
        using var fixture = new Fixture(replica: true);
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        var source = fixture.Terrain.GetChunkAtCoords(0, 0)!;
        source.HasContents = hasContents;
        var allocation = new ChunkAllocationId(source.Coords, source.AllocationGeneration);
        fixture.Transport!.Request([new ChunkContentRequest(allocation)]);
        var releasedBeforeDisposal = false;
        fixture.Manager.ChunkReleasing += chunk => releasedBeforeDisposal = !chunk.IsDisposed;

        var result = fixture.Update([Location(10)]);

        Assert.True(result.Changed);
        Assert.False(result.RetryRequired);
        Assert.Null(fixture.Terrain.GetChunkAtCoords(0, 0));
        Assert.True(source.IsDisposed);
        Assert.True(releasedBeforeDisposal);
        Assert.False(fixture.Transport.IsRequested(allocation));
        Assert.Equal(1, fixture.Discarded);
    }

    [Fact]
    public void ClosingDuringDiscardStopsReleaseAndAllocationAndAllowsTheAccessOwnerToUnwind()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        var source = fixture.Terrain.GetChunkAtCoords(0, 0)!;
        fixture.OnDiscarding = _ => fixture.World.Project.Dispose();
        var released = 0;
        fixture.Manager.ChunkReleasing += _ => released++;

        var result = fixture.Update([Location(10)]);

        Assert.False(result.RetryRequired);
        Assert.True(fixture.World.Project.IsDisposed);
        Assert.True(source.IsDisposed);
        Assert.Equal(1, fixture.Discarded);
        Assert.Equal(0, released);
        Assert.Null(fixture.Terrain.GetChunkAtCoords(10, 0));
        Assert.Equal(0, fixture.Saves!.OutstandingCount);
    }

    [Fact]
    public void CallbackReentryDefersAnotherPassWithoutRepeatingTheUnloadTransaction()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        var nested = default(TerrainResidencyManager.UpdateResult);
        fixture.OnDiscarding = _ => nested = fixture.Manager.Update([Location(10)], fixture.LocationLease);

        var result = fixture.Update([Location(10)]);

        Assert.True(nested.RetryRequired);
        Assert.True(result.RetryRequired);
        Assert.Equal(1, fixture.Manager.DeferredUpdates);
        Assert.Equal(1, fixture.Discarded);
        Assert.False(fixture.Update([Location(10)]).RetryRequired);
        Assert.Equal(1, fixture.Discarded);
        Assert.Equal(1, fixture.Manager.DeferredUpdates);
    }

    [Fact]
    public void UpdatesRequireTheWorldsExistingAccessGate()
    {
        using var fixture = new Fixture();
        Assert.Throws<SynchronizationLockException>(() => fixture.Manager.Update([Location(0)], fixture.LocationLease));
        Assert.Empty(fixture.Terrain.AllocatedChunks);
    }

    [Fact]
    public void DiscardCallbacksAllowAnotherThreadToTakeShortAccessAndKeepThePausePending()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        fixture.OnDiscarding = chunk =>
        {
            Assert.False(fixture.Subsystem.Access.IsHeldByCurrentThread);
            Assert.False(fixture.LocationLease.HasAccess);
            Assert.True(fixture.LocationLease.IsPending);
            Task.Run(() =>
                {
                    Assert.True(fixture.Subsystem.WorkExecutor.TryEnterMain(2000));
                    try { chunk.ModificationCounter++; }
                    finally { fixture.Subsystem.WorkExecutor.LeaveMain(); }
                }
            ).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Assert.True(fixture.LocationLease.IsPending);
        };
        fixture.Update([Location(10)]);
        Assert.Equal(1, fixture.Discarded);
        Assert.False(fixture.LocationLease.HasAccess);
        Assert.False(fixture.LocationLease.IsPending);
    }

    [Fact]
    public void NestedDemandApplicationDoesNotConsumeTheYieldedLocationRequest()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        using var demands = new TerrainDemandManager(fixture.Terrain, TerrainContentRole.Authority);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Demands))!.SetValue(fixture.Subsystem, demands);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Residency))!.SetValue(
            fixture.Subsystem,
            fixture.Manager
        );
        typeof(SubsystemTerrain).GetField("_locationRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fixture.Subsystem, fixture.LocationLease);
        fixture.OnDiscarding = _ =>
        {
            demands.SetLocation(0, new Vector2(320, 0), 8, 8);
            typeof(SubsystemTerrain).GetMethod("ApplyTerrainDemand", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(fixture.Subsystem, null);
            Assert.True(fixture.LocationLease.IsPending);
            Assert.False(fixture.LocationLease.HasAccess);
            Assert.True(demands.HasChanges);
        };
        fixture.Update([Location(10)]);
        Assert.True(demands.HasChanges);
        Assert.False(fixture.LocationLease.IsPending);
        Assert.Equal(1, fixture.Discarded);
    }

    [Fact]
    public void AnExtraOuterLeaseIsRejectedBeforeAnyDiscardCallback()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        fixture.Subsystem.WorkExecutor.EnterMain();
        try
        {
            Assert.Throws<SynchronizationLockException>(() => fixture.Update([Location(10)]));
            Assert.True(fixture.Subsystem.Access.IsHeldByCurrentThread);
            Assert.Equal(0, fixture.Discarded);
        }
        finally { fixture.Subsystem.WorkExecutor.LeaveMain(); }

        fixture.Update([Location(10)]);
        Assert.Equal(1, fixture.Discarded);
    }

    [Fact]
    public void AThrowingDiscardCallbackClosesTheWorldAndReleasesTheReservation()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        var failure = new InvalidOperationException("Discard failed.");
        fixture.OnDiscarding = _ => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => fixture.Update([Location(10)])));
        Assert.True(fixture.World.Project.IsDisposed);
        Assert.Equal(0, fixture.Saves!.OutstandingCount);
        Assert.False(fixture.LocationLease.HasAccess);
        Assert.False(fixture.LocationLease.IsPending);
        Assert.Equal(1, fixture.Discarded);
    }

    [Fact]
    public void CallbackCannotCancelTheUnloadLeaseAndLeaveTheWorldRunning()
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        fixture.OnDiscarding = _ => fixture.LocationLease.Dispose();

        Assert.Throws<InvalidOperationException>(() => fixture.Update([Location(10)]));
        Assert.True(fixture.World.Project.IsDisposed);
        Assert.Equal(0, fixture.Saves!.OutstandingCount);
        Assert.False(fixture.LocationLease.HasAccess);
        Assert.False(fixture.LocationLease.IsPending);
        Assert.Equal(1, fixture.Discarded);
        fixture.Manager.Update([Location(10)], fixture.LocationLease);
        Assert.Equal(1, fixture.Discarded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackCannotReplaceTheReservedAllocation(bool free)
    {
        using var fixture = new Fixture();
        // Isolate the allocation whose unload transaction is under test.
        fixture.Terrain.AllocateChunk(0, 0);
        fixture.OnDiscarding = chunk =>
        {
            if (free) { fixture.Terrain.FreeChunk(chunk); }
            else { fixture.Terrain.RenewChunkAllocation(chunk); }
        };
        Assert.Throws<InvalidOperationException>(() => fixture.Update([Location(10)]));
        Assert.True(fixture.World.Project.IsDisposed);
        Assert.Equal(0, fixture.Saves!.OutstandingCount);
        Assert.False(fixture.LocationLease.HasAccess);
        Assert.False(fixture.LocationLease.IsPending);
        Assert.Equal(1, fixture.Discarded);
    }

    private static ClientChunkSnapshot Snapshot(TerrainChunk source, ChunkAllocationId allocation)
    {
        return new ClientChunkSnapshot(
            allocation,
            source.NetworkContentVersion,
            source.Cells.ToArray(),
            source.Shafts.ToArray()
        );
    }

    private static TerrainUpdateLocation Location(int x)
    {
        return new TerrainUpdateLocation
        {
            Center = new Vector2(x * 16 + 8, 8), VisibilityDistance = 8, ContentDistance = 8
        };
    }

    private sealed class Fixture : IDisposable
    {
        public WorldCatalogScope World { get; }

        public SubsystemTerrain Subsystem { get; }

        public Terrain Terrain { get; } = new();

        public TerrainResidencyManager Manager { get; }

        public TerrainMainWorkRequest LocationLease { get; }

        public TerrainSaveCoordinator? Saves { get; }

        public ClientChunkContentTransport? Transport { get; }

        public ClientChunkContentCoordinator? Contents { get; }

        public ConcurrentQueue<int> Writes { get; } = new();

        public int Discarded { get; private set; }

        public Action<TerrainChunk>? OnDiscarding { get; set; }

        public Fixture(bool replica = false, Action<Point2, int[], long[]>? writer = null)
        {
            if (replica)
            {
                var node = new NetNode(NetNodeOptions.Default, true, PackageHandlerCatalog.Empty, NodeRole.Client)
                {
                    Server = new Client(Guid.Empty)
                };
                World = new WorldCatalogScope(node);
                Transport = new ClientChunkContentTransport(node.Runtime);
            }
            else { World = new WorldCatalogScope(); }

            Subsystem = new SubsystemTerrain { Terrain = Terrain };
            World.Project.AddSubsystem(Subsystem);
            LocationLease = Subsystem.WorkExecutor.CreateMainRequest();
            var behavior = new DiscardBehavior(chunk =>
                {
                    Discarded++;
                    OnDiscarding?.Invoke(chunk);
                }
            );
            World.Project.AddSubsystem(behavior);
            behavior.Load(new ValuesDictionary());
            var behaviors = new SubsystemBlockBehaviors();
            World.Project.AddSubsystem(behaviors);
            behaviors.Load(new ValuesDictionary());
            if (replica)
            {
                Contents = new ClientChunkContentCoordinator(Terrain);
                Set(nameof(SubsystemTerrain.ClientChunkContents), Contents);
                Set(nameof(SubsystemTerrain.ChunkContentTransport), Transport!);
                Set(nameof(SubsystemTerrain.ClientTerrainDeltas), new ClientTerrainDeltaCoordinator(Terrain, _ => { }));
            }
            else
            {
                Saves = new TerrainSaveCoordinator(writer ?? ((_, cells, _) => Writes.Enqueue(cells[0])), 1);
                Set(nameof(SubsystemTerrain.TerrainSaveCoordinator), Saves);
            }

            Manager = new TerrainResidencyManager(Subsystem, new TerrainChunkBehaviorNotifier(Subsystem, behaviors));
        }

        public TerrainResidencyManager.UpdateResult Update(TerrainUpdateLocation[] locations)
        {
            if (Manager.IsUpdating) { return Manager.Update(locations, LocationLease); }

            Assert.True(LocationLease.TryEnter(Timeout.Infinite));
            try { return Manager.Update(locations, LocationLease); }
            finally
            {
                if (LocationLease.HasAccess) { LocationLease.Leave(); }
            }
        }

        private void Set(string property, object value)
        {
            typeof(SubsystemTerrain).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(
                Subsystem,
                value
            );
        }

        public void Dispose() => World.Dispose();
    }

    private sealed class DiscardBehavior(Action<TerrainChunk> discarding) : SubsystemBlockBehavior
    {
        public override ResourceId[] HandledBlocks => [];

        public override void OnChunkDiscarding(TerrainChunk chunk) => discarding(chunk);
    }
}
