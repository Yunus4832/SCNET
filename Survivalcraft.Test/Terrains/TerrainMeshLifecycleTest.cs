using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainMeshLifecycleTest
{
    [Fact]
    public void MaintenanceWaitsForQuietInputsButInitialGeometryAndValidSecondPassDoNotWait()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.PrepareLighting(chunk);
        var mesh = presentations.Get(chunk);
        Assert.False(mesh.ShouldDeferMaintenance(chunk, 0, out _));
        TerrainMeshTestData.Complete(chunk, mesh);
        mesh.MarkUploaded();
        mesh.RequestMeshRebuild();
        Assert.True(mesh.ShouldDeferMaintenance(chunk, 1, out var due));
        Assert.Equal(1.05, due, 6);
        Assert.True(mesh.ShouldDeferMaintenance(chunk, 1.049, out _));
        Assert.False(mesh.ShouldDeferMaintenance(chunk, 1.051, out _));
        mesh.PrepareMeshUpdate(chunk);
        mesh.CompleteFirstPass();
        Assert.False(mesh.ShouldDeferMaintenance(chunk, 1.052, out _));
    }

    [Fact]
    public void NeighborLightWritesRestartQuietTimeWithoutLosingTheMaximumMaintenanceDeadline()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var mesh = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, mesh);
        mesh.MarkUploaded();
        mesh.RequestMeshRebuild();
        Assert.True(mesh.ShouldDeferMaintenance(chunk, 0, out _));
        for (var i = 1; i <= 4; i++)
        {
            neighbor.DerivedRevisions.RecordLightChange();
            Assert.True(mesh.ShouldDeferMaintenance(chunk, i * .04, out var due));
            Assert.InRange(due, i * .04, .2);
        }

        neighbor.DerivedRevisions.RecordLightChange();
        Assert.False(mesh.ShouldDeferMaintenance(chunk, .2, out _));
    }

    [Fact]
    public void TemporaryLightWriterDoesNotInvalidateUnchangedGpuInputsButCannotPublishANewCpuBuild()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var mesh = presentations.Get(target);
        TerrainMeshTestData.Complete(target, mesh);
        mesh.MarkUploaded();
        neighbor.DerivedRevisions.BeginLightWrite();
        Assert.False(neighbor.HasCurrentLight);
        Assert.True(mesh.HasCurrentGeometry(target));
        Assert.True(mesh.HasCurrentUploadedGeometry(target));
        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(target);
        mesh.CompleteFirstPass();
        Assert.False(mesh.CompleteMeshBuild(target));
        neighbor.DerivedRevisions.EndLightWrite();
        Assert.True(mesh.CompleteMeshBuild(target));
        Assert.False(mesh.HasCurrentUploadedGeometry(target));
        mesh.MarkUploaded();
        Assert.True(mesh.HasCurrentUploadedGeometry(target));
    }

    [Fact]
    public void PendingNeighborInvalidationRejectsFreshnessBeforeAnyLightValueChanges()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var mesh = presentations.Get(target);
        TerrainMeshTestData.Complete(target, mesh);
        mesh.MarkUploaded();
        Assert.True(mesh.HasCurrentGeometry(target));
        neighbor.DerivedRevisions.InvalidateLight();
        Assert.False(mesh.HasCurrentGeometry(target));
        Assert.False(mesh.HasCurrentUploadedGeometry(target));
        mesh.MarkUploaded();
        Assert.True(mesh.GeometryUploaded);
        mesh.PrepareMeshUpdate(target);
        mesh.CompleteFirstPass();
        Assert.False(mesh.CompleteMeshBuild(target));
    }

    [Fact]
    public void NeighborLightingChangeRejectsAnInFlightMeshWithoutChangingContent()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var target = terrain.AllocateChunk(0, 0);
        target.NetworkContentVersion = 4;
        target.HasContents = true;
        var neighbor = terrain.AllocateChunk(1, 0);
        neighbor.HasContents = true;
        TerrainMeshTestData.PrepareLighting(target);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var mesh = presentations.Get(target);
        mesh.PrepareMeshUpdate(target);
        mesh.CompleteFirstPass();
        neighbor.DerivedRevisions.RecordLightChange();
        Assert.False(mesh.CompleteMeshBuild(target));
        Assert.Equal(4, target.NetworkContentVersion);
        TerrainMeshTestData.Complete(target, mesh);
        Assert.True(mesh.HasCurrentGeometry(target));
        neighbor.DerivedRevisions.RecordLightChange();
        Assert.False(mesh.HasCurrentGeometry(target));
    }

    [Fact]
    public void CpuCompletionCannotAdvanceTheActuallyUploadedVersion()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.NetworkContentVersion = 4;
        chunk.HasContents = true;
        var mesh = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.Equal(0, mesh.UploadedContentVersion);
        mesh.MarkUploaded();
        Assert.Equal(4, mesh.UploadedContentVersion);
        Assert.True(mesh.HasCurrentUploadedGeometry(chunk));
        chunk.NetworkContentVersion = 5;
        Assert.False(mesh.HasCurrentUploadedGeometry(chunk));
        Assert.True(mesh.GeometryUploaded);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.Equal(5, mesh.ClientGeometryContentVersion);
        Assert.Equal(4, mesh.UploadedContentVersion);
        mesh.MarkUploaded();
        Assert.Equal(5, mesh.UploadedContentVersion);
        chunk.DerivedRevisions.RecordLightChange();
        // A lighting invalidation during upload does not erase the actual upload record.
        mesh.MarkUploaded();
        Assert.Equal(5, mesh.UploadedContentVersion);
        Assert.False(mesh.HasCurrentUploadedGeometry(chunk));
    }

    [Fact]
    public void MeshRebuildLeavesCpuReadyAndUploadedGeometryAvailable()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 3;


        var presentation = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, presentation);
        presentation.GeometryUploaded = true;

        presentation.RequestMeshRebuild();

        Assert.True(chunk.HasCurrentHeightData);
        Assert.True(chunk.HasCurrentLight);
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
    public void ChunkGeometryUsesSixteenSlicesFor256BlockHeight()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);

        Assert.Equal(16, TerrainChunk.SlicesCount);
        Assert.Equal(TerrainChunk.SlicesCount, presentations.Get(chunk).ChunkSliceGeometries.Length);
        Assert.Equal(TerrainChunk.SlicesCount, presentations.Get(chunk).GeneratedSliceContentsHashes.Length);
    }
}
