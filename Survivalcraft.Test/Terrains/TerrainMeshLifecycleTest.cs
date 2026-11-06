using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainMeshLifecycleTest
{
    [Fact]
    public void NearestMeshRetainsItsLoadedNeighborDependencyOutsideVisibility()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var target = terrain.AllocateChunk(0, 0);
        target.IsLoaded = true;
        target.SetWorkerState(TerrainChunkState.Ready);
        var neighbor = terrain.AllocateChunk(1, 0);
        neighbor.IsLoaded = true;
        neighbor.SetWorkerState(TerrainChunkState.InvalidLight);
        var locations = new[]
        {
            new TerrainUpdateLocation { Center = target.Center, VisibilityDistance = 8, ContentDistance = 8 }
        };
        Assert.Same(target, TerrainMeshUpdater.SelectMeshChunkToUpdate([target, neighbor], locations, presentations));
        Assert.Same(
            neighbor,
            ClientDerivedTerrainPolicy.FindPendingGeometryDependency(terrain, TerrainContentRole.Replica, target)
        );
        Assert.True(TerrainUpdater.PreferDerivedWork(null, target, locations));
        neighbor.SetWorkerState(TerrainChunkState.Ready);
        Assert.Null(
            ClientDerivedTerrainPolicy.FindPendingGeometryDependency(terrain, TerrainContentRole.Replica, target)
        );
    }

    [Fact]
    public void MeshRebuildLeavesCpuReadyAndUploadedGeometryAvailable()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 3;
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        var presentation = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, presentation);
        presentation.GeometryUploaded = true;

        presentation.RequestMeshRebuild();

        Assert.Equal(TerrainChunkState.Ready, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.Ready, chunk.WorkerState);
        Assert.False(presentation.HasCurrentGeometry(chunk));
        Assert.True(presentation.GeometryUploaded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildCannotPublishAfterContentOrRebuildRequestChanges(bool contentChanged)
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.NetworkContentVersion = 1;
        var presentation = presentations.Get(chunk);
        presentation.PrepareMeshUpdate(chunk);
        presentation.CompleteFirstPass();
        if (contentChanged) { chunk.NetworkContentVersion = 2; }
        else { presentation.RequestMeshRebuild(); }

        Assert.False(presentation.CompleteMeshBuild(chunk));
        Assert.False(presentation.NewGeometryData);
        Assert.Equal(TerrainMeshState.NeedsFirstPass, presentation.PrepareMeshUpdate(chunk));
    }

    [Fact]
    public void AllocationRenewalRejectsBuildStartedBeforeRenewal()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var generation = chunk.AllocationGeneration;
        var presentation = presentations.Get(chunk);
        presentation.PrepareMeshUpdate(chunk);
        presentation.CompleteFirstPass();
        terrain.RenewChunkAllocation(chunk);
        presentations.RenewAllocation(chunk, generation);

        Assert.False(presentation.CompleteMeshBuild(chunk));
        Assert.False(presentation.HasCurrentGeometry(chunk));
    }

    [Fact]
    public void CpuSchedulerFinishesIndependentlyWhileClientMeshSchedulerWaitsForCpu()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.SetWorkerState(TerrainChunkState.InvalidLight);
        var locations = new Dictionary<int, TerrainUpdateLocation>
        {
            [0] = new() { Center = Vector2.Zero, VisibilityDistance = 64, ContentDistance = 64 }
        };
        Assert.Null(TerrainMeshUpdater.SelectMeshChunkToUpdate([chunk], locations.Values, presentations));
        chunk.SetWorkerState(TerrainChunkState.Ready);

        Assert.Null(
            TerrainUpdater.SelectChunkToUpdate([chunk], locations.Values, TerrainContentRole.Authority, false, out _)
        );
        Assert.Null(
            TerrainUpdater.SelectChunkToUpdate([chunk], locations.Values, TerrainContentRole.Replica, false, out _)
        );
        Assert.Same(chunk, TerrainMeshUpdater.SelectMeshChunkToUpdate([chunk], locations.Values, presentations));
    }
}
