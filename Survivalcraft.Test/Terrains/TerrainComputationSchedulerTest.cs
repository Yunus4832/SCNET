using Engine.Core.Mathematics;

using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainComputationSchedulerTest
{
    [Fact]
    public void ExternallyDeferredGeometryDoesNotSpinAndOnlyTheLiveAllocationCanResumeIt()
    {
        using var fixture = new ComputationWorld();
        fixture.Populate();
        var work = new WaitingDerived();
        fixture.Scheduler.AttachDerivedWork(work);
        fixture.Scheduler.UpdateDemand([Location()]);
        fixture.Drain();
        Assert.Equal(9, work.Calls);
        Assert.Equal(9, fixture.Scheduler.Counts.Waiting);
        Assert.True(fixture.Scheduler.Step());
        var chunk = fixture.Data.GetChunkAtCoords(0, 0)!;
        fixture.Scheduler.ResumeDerived(chunk, chunk.AllocationGeneration + 1);
        Assert.True(fixture.Scheduler.Step());
        work.Available = true;
        foreach (var current in fixture.Data.AllocatedChunks)
        {
            fixture.Scheduler.ResumeDerived(current, current.AllocationGeneration);
        }

        fixture.Drain();
        Assert.Equal(0, fixture.Scheduler.Counts.Total);
    }

    private sealed class WaitingDerived : ITerrainDerivedWork
    {
        public bool Available { get; set; }

        public int Calls { get; private set; }

        public bool HasCurrentGeometry(TerrainChunk chunk) => Available;

        public bool HasUploadedGeometry(TerrainChunk chunk) => true;

        public TerrainDerivedWorkResult UpdateChunk(TerrainChunk chunk)
        {
            Calls++;
            return TerrainDerivedWorkResult.Waiting;
        }
    }

    [Fact]
    public void ContentInvalidationPreservesNeighborHeightsAndRebuildsOnlyRequiredInputs()
    {
        using var fixture = new ComputationWorld();
        fixture.Populate();
        fixture.Scheduler.UpdateDemand([Location()]);
        fixture.Drain();
        var center = fixture.Data.GetChunkAtCoords(0, 0)!;
        var changed = new List<TerrainChunk>();
        fixture.Scheduler.DerivedInputsInvalidated += changed.Add;

        fixture.Scheduler.InvalidateContentNeighborhood(center.Coords, 1);

        Assert.False(center.HasCurrentHeightData);
        Assert.Equal(9, changed.Count);
        Assert.All(
            fixture.Data.AllocatedChunks.Where(chunk => chunk != center),
            chunk => Assert.True(chunk.HasCurrentHeightData)
        );
        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.False(chunk.HasCurrentLight));
        fixture.Drain();
        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.True(chunk.HasCurrentLight));
    }

    [Fact]
    public void SkylightInvalidationDoesNotDiscardCurrentHeightData()
    {
        using var fixture = new ComputationWorld(authority: true);
        fixture.Populate();
        fixture.Scheduler.UpdateDemand([Location()]);
        fixture.Drain();

        fixture.Scheduler.InvalidateSkylight();

        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.True(chunk.HasCurrentHeightData));
        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.False(chunk.HasCurrentLight));
        fixture.Drain();
        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.True(chunk.HasCurrentLight));
    }

    [Fact]
    public void DerivedWorkRejectsAuthorityAndDuplicateBindingAndUnbindsOnlyTheExactOwner()
    {
        using (var authority = new ComputationWorld(authority: true))
        {
            Assert.Throws<InvalidOperationException>(() =>
                authority.Scheduler.AttachDerivedWork(new Derived(authority.Data))
            );
            Assert.False(authority.World.WorkExecutor.HasMainRequest);
        }

        using var fixture = new ComputationWorld();
        fixture.Populate();
        var derived = new Derived(fixture.Data);
        var other = new Derived(fixture.Data);
        fixture.Scheduler.AttachDerivedWork(derived);
        Assert.Throws<InvalidOperationException>(() => fixture.Scheduler.AttachDerivedWork(other));
        fixture.Scheduler.DetachDerivedWork(other);
        fixture.Scheduler.UpdateDemand([Location()]);
        fixture.Drain();
        Assert.NotEmpty(derived.Completed);
        Assert.Empty(other.Completed);

        fixture.Scheduler.DetachDerivedWork(derived);
        derived.Completed.Clear();
        fixture.Scheduler.InvalidateContentNeighborhood(new Point2(0, 0), 1);
        fixture.Drain();
        Assert.Empty(derived.Completed);
        Assert.False(fixture.World.WorkExecutor.HasMainRequest);
    }

    [Fact]
    public void CriticalContentPriorityDoesNotPersistIntoTheNextAuthorityStage()
    {
        using var fixture = new ComputationWorld(authority: true);
        var near = fixture.Data.AllocateChunk(0, 0);
        var distant = fixture.Data.AllocateChunk(8, 0);
        distant.HasContents = true;
        distant.ContentOrigin = TerrainContentOrigin.Storage;
        fixture.Scheduler.UpdateDemand(
            [
                new TerrainUpdateLocation
                {
                    Center = near.Center, VisibilityDistance = 256, ContentDistance = 256
                }
            ]
        );
        near.HasContents = true;
        near.ContentOrigin = TerrainContentOrigin.Storage;

        Assert.False(fixture.Scheduler.Step());
        Assert.False(near.HasCurrentHeightData);
        Assert.False(fixture.Scheduler.Step());
        Assert.True(near.HasCurrentHeightData);
        Assert.False(distant.HasCurrentHeightData);
    }

    [Fact]
    public void EstablishedAuthorityMaintenanceKeepsAServiceTurnAlongsideInitialWork()
    {
        using var fixture = new ComputationWorld(authority: true);
        var established = fixture.Data.AllocateChunk(0, 0);
        established.HasContents = true;
        established.IsContentInitialized = true;
        established.ContentOrigin = TerrainContentOrigin.Storage;
        var initial = fixture.Data.AllocateChunk(8, 0);
        initial.HasContents = true;
        initial.ContentOrigin = TerrainContentOrigin.Storage;
        fixture.Scheduler.UpdateDemand(
            [
                new TerrainUpdateLocation
                {
                    Center = established.Center, VisibilityDistance = 256, ContentDistance = 256
                }
            ]
        );

        Assert.False(fixture.Scheduler.Step());
        Assert.True(initial.HasCurrentHeightData);
        Assert.False(established.HasCurrentHeightData);
        Assert.False(fixture.Scheduler.Step());
        Assert.True(established.HasCurrentHeightData);
    }

    [Fact]
    public void NearbyInitialAuthorityDerivationPrecedesDistantContentGeneration()
    {
        using var fixture = new ComputationWorld(authority: true);
        var near = fixture.Data.AllocateChunk(0, 0);
        near.HasContents = true;
        near.ContentOrigin = TerrainContentOrigin.Storage;
        var distant = fixture.Data.AllocateChunk(8, 0);
        fixture.Scheduler.UpdateDemand(
            [
                new TerrainUpdateLocation
                {
                    Center = near.Center, VisibilityDistance = 256, ContentDistance = 256
                }
            ]
        );

        Assert.False(fixture.Scheduler.Step());

        Assert.True(near.HasCurrentHeightData);
        Assert.False(near.IsContentInitialized);
        Assert.False(distant.HasContents);
    }

    [Fact]
    public void AuthorityIgnoresUnpublishedNeighborDataAndReconcilesWhenThatContentArrives()
    {
        using var fixture = new ComputationWorld(authority: true);
        fixture.Populate();
        var center = fixture.Data.GetChunkAtCoords(0, 0)!;
        center.SetCellValueFast(15, 64, 8, 3);
        var neighbor = fixture.Data.GetChunkAtCoords(1, 0)!;
        neighbor.HasContents = false;
        Array.Fill(neighbor.Shafts, long.MaxValue);
        for (var y = 0; y < 256; y++)
        {
            neighbor.SetCellLightFast(TerrainChunk.CalculateCellIndex(0, y, 0), 15);
        }

        var unpublishedCells = neighbor.Cells.ToArray();
        var unpublishedShafts = neighbor.Shafts.ToArray();
        fixture.Scheduler.UpdateDemand([Location()]);
        fixture.Drain();
        Assert.Equal(TerrainContentRole.Authority, fixture.World.ContentRole);
        Assert.Null(fixture.World.AuthorityContents);
        Assert.Equal(unpublishedCells, neighbor.Cells);
        Assert.Equal(unpublishedShafts, neighbor.Shafts);

        using (var missing = new ComputationWorld(authority: true))
        {
            missing.Populate();
            missing.Data.GetChunkAtCoords(0, 0)!.SetCellValueFast(15, 64, 8, 3);
            missing.Data.GetChunkAtCoords(1, 0)!.HasContents = false;
            missing.Scheduler.UpdateDemand([Location()]);
            missing.Drain();
            Assert.Equal(missing.Data.GetChunkAtCoords(0, 0)!.Cells, center.Cells);
            Assert.Equal(missing.Data.GetChunkAtCoords(0, 0)!.Shafts, center.Shafts);
        }

        Array.Clear(neighbor.Cells);
        Array.Clear(neighbor.Shafts);
        neighbor.SetCellValueFast(0, 64, 0, 3);
        neighbor.ContentOrigin = TerrainContentOrigin.Storage;
        neighbor.HasContents = true;
        fixture.Scheduler.ContentAvailable(neighbor);
        fixture.Drain();
        using var simultaneous = new ComputationWorld(authority: true);
        simultaneous.Populate();
        simultaneous.Data.GetChunkAtCoords(0, 0)!.SetCellValueFast(15, 64, 8, 3);
        simultaneous.Data.GetChunkAtCoords(1, 0)!.SetCellValueFast(0, 64, 0, 3);
        simultaneous.Scheduler.UpdateDemand([Location()]);
        simultaneous.Drain();
        foreach (var chunk in fixture.Data.AllocatedChunks)
        {
            var expected = simultaneous.Data.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y)!;
            Assert.Equal(expected.Cells, chunk.Cells);
            Assert.Equal(expected.Shafts, chunk.Shafts);
            Assert.True(chunk.HasCurrentLight);
        }
    }

    [Fact]
    public void MissingReplicaContentSleepsUntilActualInstallationWithoutGeneratingLocally()
    {
        using var fixture = new ComputationWorld();
        var target = fixture.Data.AllocateChunk(0, 0);
        fixture.Scheduler.UpdateDemand([Location()]);
        Assert.False(fixture.Scheduler.Step());
        Assert.True(fixture.Scheduler.Step());
        Assert.Equal((1, 0, 1), fixture.Scheduler.Counts);
        target.HasContents = true;
        target.ContentOrigin = TerrainContentOrigin.Replica;
        fixture.Scheduler.ContentAvailable(target);
        fixture.Drain();
        Assert.True(target.HasCurrentLight);
        Assert.Null(fixture.World.AuthorityContents);
        Assert.Equal((0, 0, 0), fixture.Scheduler.Counts);
    }

    [Fact]
    public void NeighborDependenciesFinishLightingBeforeClientDerivedWorkWithoutFinalReadyCycles()
    {
        using var fixture = new ComputationWorld();
        fixture.Populate();
        var center = fixture.Data.GetChunkAtCoords(0, 0)!;
        center.SetCellValueFast(15, 64, 8, 3);
        var derived = new Derived(fixture.Data);
        fixture.Scheduler.AttachDerivedWork(derived);
        fixture.Scheduler.UpdateDemand(
            [
                new TerrainUpdateLocation
                {
                    Center = center.Center, VisibilityDistance = 8, ContentDistance = 8
                }
            ]
        );
        fixture.Drain();
        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.True(chunk.HasCurrentLight));
        Assert.Equal(14, fixture.Data.GetChunkAtCoords(1, 0)!.GetCellLightFast(0, 64, 8));
        Assert.NotEmpty(derived.Completed);
        Assert.Equal((0, 0, 0), fixture.Scheduler.Counts);
    }

    [Fact]
    public void InvalidatingAResumablePropagationReleasesWriteScopesAndRecomputesTheLatestInput()
    {
        using var fixture = new ComputationWorld();
        fixture.Populate();
        var center = fixture.Data.GetChunkAtCoords(0, 0)!;
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++) { center.SetCellValueFast(x, 64, z, 3); }
        }

        fixture.Scheduler.UpdateDemand([Location()]);
        for (var step = 0; step < 1000 && !fixture.Calculator.HasActivePropagation; step++)
        {
            Assert.False(fixture.Scheduler.Step());
        }

        Assert.True(fixture.Calculator.HasActivePropagation);
        Assert.Contains(fixture.Data.AllocatedChunks, chunk => !chunk.HasCurrentLight);
        center.SetCellValueFast(8, 64, 8, 0);
        center.DerivedRevisions.InvalidateHeightsAndLight();
        fixture.Scheduler.Request(center);
        fixture.Drain();
        Assert.False(fixture.Calculator.HasActivePropagation);
        Assert.All(fixture.Data.AllocatedChunks, chunk => Assert.True(chunk.HasCurrentLight));
        Assert.Equal((0, 0, 0), fixture.Scheduler.Counts);
    }

    [Fact]
    public void CancellingDemandAndReturningToTheSameContentsDoesNotLoseTheRemainingLightFrontier()
    {
        using var fixture = new ComputationWorld();
        fixture.Populate();
        var center = fixture.Data.GetChunkAtCoords(0, 0)!;
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++) { center.SetCellValueFast(x, 64, z, 3); }
        }

        fixture.Scheduler.UpdateDemand([Location()]);
        for (var step = 0; step < 1000 && !fixture.Calculator.HasActivePropagation; step++)
        {
            Assert.False(fixture.Scheduler.Step());
        }

        Assert.True(fixture.Calculator.HasActivePropagation);
        fixture.Scheduler.UpdateDemand([]);
        Assert.True(fixture.Scheduler.Step());
        Assert.False(fixture.Calculator.HasActivePropagation);
        fixture.Scheduler.UpdateDemand([Location()]);
        fixture.Drain();
        using var reference = new ComputationWorld();
        reference.Populate();
        var other = reference.Data.GetChunkAtCoords(0, 0)!;
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++) { other.SetCellValueFast(x, 64, z, 3); }
        }

        reference.Scheduler.UpdateDemand([Location()]);
        reference.Drain();
        foreach (var chunk in fixture.Data.AllocatedChunks)
        {
            Assert.Equal(reference.Data.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y)!.Cells, chunk.Cells);
            Assert.True(chunk.HasCurrentLight);
        }
    }

    private static TerrainUpdateLocation Location() => new()
    {
        Center = new Vector2(8, 8), VisibilityDistance = 64, ContentDistance = 64
    };

    private sealed class Derived(Terrain terrain) : ITerrainDerivedWork
    {
        public HashSet<TerrainChunk> Completed { get; } = [];

        public bool HasCurrentGeometry(TerrainChunk chunk) => Completed.Contains(chunk);

        public bool HasUploadedGeometry(TerrainChunk chunk) => false;

        public TerrainDerivedWorkResult UpdateChunk(TerrainChunk chunk)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var z = -1; z <= 1; z++)
                {
                    var neighbor = terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
                    if (neighbor is { HasContents: true }) { Assert.True(neighbor.HasCurrentLight); }
                }
            }

            Completed.Add(chunk);
            return TerrainDerivedWorkResult.Complete;
        }
    }

    private sealed class ComputationWorld : IDisposable
    {
        private readonly WorldCatalogScope _scope;

        public Terrain Data { get; } = new();

        public SubsystemTerrain World { get; }

        public TerrainLightingCalculator Calculator { get; }

        public TerrainComputationScheduler Scheduler { get; }

        public ComputationWorld(bool authority = false)
        {
            _scope = new WorldCatalogScope(
                authority ? Game.World.RuntimeSide.Authority : Game.World.RuntimeSide.Client
            );
            _scope.Catalog.Blocks[0].Transparent = true;
            _scope.Catalog.Blocks[0].LightAttenuation = 0;
            _scope.Catalog.Blocks[3].Transparent = true;
            _scope.Catalog.Blocks[3].LightAttenuation = 0;
            _scope.Catalog.Blocks[3].EmittedLightAmount = 15;
            World = new SubsystemTerrain { Terrain = Data };
            _scope.Project.InitializeSubsystem(World);
            _scope.Project.AddSubsystem(new SubsystemSky());
            if (!authority)
            {
                typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.ClientChunkContents))!
                    .SetValue(World, new ClientChunkContentCoordinator(Data));
            }

            Calculator = new TerrainLightingCalculator(
                Data,
                _scope.Catalog,
                !authority,
                chunk => chunk.HasContents && !chunk.NeedsContentValidation
            );
            Scheduler = new TerrainComputationScheduler(World, Calculator);
        }

        public void Populate()
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var z = -1; z <= 1; z++)
                {
                    var chunk = Data.AllocateChunk(x, z);
                    chunk.HasContents = true;
                    chunk.ContentOrigin = World.ContentRole == TerrainContentRole.Authority
                        ? TerrainContentOrigin.Storage
                        : TerrainContentOrigin.Replica;
                }
            }
        }

        public void Drain()
        {
            for (var i = 0; i < 20000; i++)
            {
                if (Scheduler.Step()) { return; }
            }

            Assert.Fail("Terrain stage queue did not converge within its deterministic work bound.");
        }

        public void Dispose()
        {
            Scheduler.Dispose();
            World.WorkExecutor.Dispose();
            Data.Dispose();
            _scope.Dispose();
        }
    }
}
