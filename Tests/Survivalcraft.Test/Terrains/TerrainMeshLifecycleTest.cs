using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainMeshLifecycleTest
{
    [Fact]
    public void RebuildingASubsetClearsThePreviousTorchIndexRanges()
    {
        var subset = new TerrainGeometrySubset();
        subset.Indices.Add(0);
        subset.Indices.Add(1);
        subset.Indices.Add(2);
        subset.RecordRemovableBlock(0, 123);
        Assert.Equal(new TerrainBlockIndexRange(0, 3, 123), Assert.Single(subset.RemovableBlocks!));

        subset.ClearGeometry();
        Assert.Empty(subset.RemovableBlocks!);
        Assert.Empty(subset.Indices);
    }

    [Fact]
    public void PickableLightingWaitsForAuthoritativeMeshUpload()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.False(mesh.HasCurrentUploadedLighting(chunk));
        mesh.MarkUploaded();
        Assert.True(mesh.HasCurrentUploadedLighting(chunk));

        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(chunk, true);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        mesh.MarkUploaded();
        Assert.False(mesh.HasCurrentUploadedLighting(chunk));

        mesh.RequestMeshRebuild();
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.False(mesh.HasCurrentUploadedLighting(chunk));
        mesh.MarkUploaded();
        Assert.True(mesh.HasCurrentUploadedLighting(chunk));
    }

    [Fact]
    public void InteractiveBuildPinsLightWhileGeometryChangesStillRestartIt()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        var first = new TerrainLightSnapshot(new TerrainLightChannels(15, 0));
        Assert.True(terrain.Lighting.TryPublish(chunk, terrain.Lighting.CaptureInput(chunk), first));
        mesh.MarkUploaded();
        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(chunk, true);
        mesh.CompleteMeshSlice();
        Assert.Same(first, mesh.BuildLights![4]);
        var second = new TerrainLightSnapshot(new TerrainLightChannels(0, 15));
        Assert.True(terrain.Lighting.TryPublish(chunk, terrain.Lighting.CaptureInput(chunk), second));
        Assert.True(chunk.DerivedRevisions.PublishLight(chunk.DerivedRevisions.Capture()));
        mesh.PrepareMeshUpdate(chunk, true);
        Assert.Equal(1, mesh.CompletedMeshSlices);
        Assert.Same(first, mesh.BuildLights![4]);
        chunk.NetworkContentVersion++;
        mesh.PrepareMeshUpdate(chunk, true);
        Assert.Equal(0, mesh.CompletedMeshSlices);
        Assert.Same(second, mesh.BuildLights![4]);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        Assert.Null(mesh.BuildLights);
        mesh.MarkUploaded();
        Assert.True(mesh.FinishInteractiveUpload());
    }

    [Fact]
    public void InteractivePreviewOnlyReusesUnchangedUploadedSlicesAndRequiresMaintenance()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        for (var slice = 0; slice < TerrainChunk.SlicesCount; slice++)
        {
            mesh.CompleteSliceGeometryUpdate(slice, new TerrainSliceInputs(slice + 100, 0));
        }

        mesh.MarkUploaded();
        mesh.MarkInteractiveCell(64);
        Assert.False(mesh.TryReuseInteractiveSlice(3));
        Assert.False(mesh.TryReuseInteractiveSlice(4));
        Assert.True(mesh.TryReuseInteractiveSlice(2));
        mesh.CompleteSliceGeometryUpdate(5, new TerrainSliceInputs(500, 0));
        Assert.False(mesh.TryReuseInteractiveSlice(5));
        Assert.True(mesh.BeginSliceGeometryUpdate(6, new TerrainSliceInputs(600, 0)));
        Assert.False(mesh.TryReuseInteractiveSlice(6));
        Assert.True(mesh.FinishInteractiveUpload());
        Assert.False(mesh.TryReuseInteractiveSlice(2));
        Assert.False(mesh.FinishInteractiveUpload());
    }

    [Fact]
    public void SnapshotRefreshDisablesInteractiveReuseOfEverySlice()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        for (var slice = 0; slice < TerrainChunk.SlicesCount; slice++)
        {
            mesh.CompleteSliceGeometryUpdate(slice, new TerrainSliceInputs(slice + 100, 0));
        }

        mesh.MarkUploaded();
        mesh.MarkInteractiveSnapshot();
        for (var slice = 0; slice < TerrainChunk.SlicesCount; slice++)
        {
            Assert.False(mesh.TryReuseInteractiveSlice(slice));
        }
    }

    [Fact]
    public void CompletedButNotUploadableGeometryCanUsePublishedLightForInteraction()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.True(
            terrain.Lighting.TryPublish(
                chunk,
                terrain.Lighting.CaptureInput(chunk),
                new TerrainLightSnapshot(new TerrainLightChannels(15, 0))
            )
        );
        mesh.MarkUploaded();
        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(chunk);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        chunk.DerivedRevisions.InvalidatePropagation();
        Assert.True(mesh.HasCurrentGeometry(chunk));
        Assert.False(mesh.HasCurrentUploadedGeometry(chunk));
        Assert.False(mesh.CanUploadGeometry(chunk));
        Assert.Equal(TerrainMeshState.Ready, mesh.PrepareMeshUpdate(chunk, true));
        Assert.True(mesh.CanUploadGeometry(chunk));
    }

    [Fact]
    public void ForegroundInteractionPromotesAnExistingBuildWithoutRestartingCompletedSlices()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.True(
            terrain.Lighting.TryPublish(
                chunk,
                terrain.Lighting.CaptureInput(chunk),
                new TerrainLightSnapshot(new TerrainLightChannels(15, 0))
            )
        );
        mesh.MarkUploaded();
        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(chunk);
        mesh.CompleteMeshSlice();
        chunk.DerivedRevisions.InvalidatePropagation();
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(chunk, true));
        Assert.Equal(1, mesh.CompletedMeshSlices);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.False(chunk.HasCurrentLight);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        Assert.True(mesh.CanUploadGeometry(chunk));
    }

    [Fact]
    public void StagedUploadTokenCannotPublishAfterInputsOrAllocationChange()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.Complete(chunk, mesh);
        var original = mesh.GeometryUploadToken!;
        Assert.True(mesh.CanPublishUpload(chunk, original));
        mesh.RequestMeshRebuild();
        Assert.False(mesh.CanPublishUpload(chunk, original));
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.NotSame(original, mesh.GeometryUploadToken);
        Assert.False(mesh.CanPublishUpload(chunk, original));
        Assert.True(mesh.CanPublishUpload(chunk, mesh.GeometryUploadToken!));
        terrain.RenewChunkAllocation(chunk);
        Assert.False(mesh.CanPublishUpload(chunk, mesh.GeometryUploadToken!));
    }

    [Fact]
    public void VisibleInteractionCanPublishGeometryWithPreviousLightAndCurrentHeights()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var mesh = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.True(
            terrain.Lighting.TryPublish(
                chunk,
                terrain.Lighting.CaptureInput(chunk),
                new TerrainLightSnapshot(new TerrainLightChannels(15, 0))
            )
        );
        mesh.MarkUploaded();
        chunk.SetCellValueFast(8, 64, 8, Terrain.MakeBlockValue(1));
        chunk.DerivedRevisions.InvalidateHeightsAndLight();
        mesh.RequestMeshRebuild();
        Assert.False(mesh.CanUsePublishedLighting(chunk));
        Assert.True(chunk.DerivedRevisions.PublishHeights(chunk.DerivedRevisions.Capture()));
        Assert.False(chunk.HasCurrentLight);
        Assert.True(mesh.CanUsePublishedLighting(chunk));
        mesh.PrepareMeshUpdate(chunk, true);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        Assert.True(mesh.CanUploadGeometry(chunk));
        mesh.MarkUploaded();
        Assert.True(mesh.HasCurrentUploadedGeometry(chunk));
        Assert.True(mesh.FinishInteractiveUpload());
        mesh.RequestMeshRebuild();
        var request = chunk.DerivedRevisions.Capture();
        Assert.True(chunk.DerivedRevisions.PublishLightSeed(request));
        Assert.True(chunk.DerivedRevisions.PublishLight(request));
        Assert.False(mesh.HasCurrentGeometry(chunk));
        Assert.False(mesh.CanUploadGeometry(chunk));
    }

    [Fact]
    public void InitialGeometryCannotUsePendingLightEvenWhenAPreviousSnapshotExists()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        TerrainMeshTestData.PrepareLighting(chunk);
        Assert.True(
            terrain.Lighting.TryPublish(
                chunk,
                terrain.Lighting.CaptureInput(chunk),
                new TerrainLightSnapshot(new TerrainLightChannels(15, 0))
            )
        );
        chunk.DerivedRevisions.InvalidatePropagation();
        var mesh = presentations.Get(chunk);
        mesh.PrepareMeshUpdate(chunk, true);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.False(mesh.CanUsePublishedLighting(chunk));
        Assert.False(mesh.CompleteMeshBuild(chunk));
        Assert.False(mesh.CanUploadGeometry(chunk));
    }

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
        TerrainMeshTestData.CompleteSlices(mesh);
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
            Assert.True(neighbor.DerivedRevisions.PublishLight(neighbor.DerivedRevisions.Capture()));
            Assert.True(mesh.ShouldDeferMaintenance(chunk, i * .04, out var due));
            Assert.InRange(due, i * .04, .2);
        }

        Assert.True(neighbor.DerivedRevisions.PublishLight(neighbor.DerivedRevisions.Capture()));
        Assert.False(mesh.ShouldDeferMaintenance(chunk, .2, out _));
    }

    [Fact]
    public void PrivateLightWorkRetainsPublishedGpuInputsButCannotPublishANewCpuBuild()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var mesh = presentations.Get(target);
        TerrainMeshTestData.Complete(target, mesh);
        mesh.MarkUploaded();
        neighbor.DerivedRevisions.InvalidatePropagation();
        Assert.False(neighbor.HasCurrentLight);
        Assert.True(mesh.HasCurrentGeometry(target));
        Assert.True(mesh.HasCurrentUploadedGeometry(target));
        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(target);
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.False(mesh.CompleteMeshBuild(target));
        Assert.True(neighbor.DerivedRevisions.PublishLight(neighbor.DerivedRevisions.Capture(), false));
        Assert.True(mesh.CompleteMeshBuild(target));
        Assert.False(mesh.HasCurrentUploadedGeometry(target));
        mesh.MarkUploaded();
        Assert.True(mesh.HasCurrentUploadedGeometry(target));
    }

    [Fact]
    public void PendingNeighborWorkRetainsPublishedGeometryButRejectsANewBuild()
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
        Assert.True(mesh.HasCurrentGeometry(target));
        Assert.True(mesh.HasCurrentUploadedGeometry(target));
        mesh.MarkUploaded();
        Assert.True(mesh.GeometryUploaded);
        mesh.PrepareMeshUpdate(target);
        TerrainMeshTestData.CompleteSlices(mesh);
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
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(neighbor.DerivedRevisions.PublishLight(neighbor.DerivedRevisions.Capture()));
        Assert.False(mesh.CompleteMeshBuild(target));
        Assert.Equal(4, target.NetworkContentVersion);
        TerrainMeshTestData.Complete(target, mesh);
        Assert.True(mesh.HasCurrentGeometry(target));
        Assert.True(neighbor.DerivedRevisions.PublishLight(neighbor.DerivedRevisions.Capture()));
        Assert.False(mesh.HasCurrentGeometry(target));
    }

    [Fact]
    public void UnsampledNeighborSectionKeepsACompletedMeshAndItsSuccessorInFlight()
    {
        using var terrain = new Terrain();
        using var mesh = new TerrainChunkPresentation();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        neighbor.ContentOrigin = TerrainContentOrigin.Replica;
        TerrainMeshTestData.PrepareLighting(target);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var input = terrain.Lighting.CaptureInput(neighbor);
        var first = new TerrainLightSnapshot(default);
        Assert.True(terrain.Lighting.TryPublish(neighbor, input, first));
        var reads = new TerrainMeshLightReads();
        reads.Add(new Point2(1, 0), 0, false);

        mesh.PrepareMeshUpdate(target);
        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            mesh.MarkSliceLightReads(i, in reads);
            mesh.CompleteSliceGeometryUpdate(i, default);
            mesh.CompleteMeshSlice();
        }

        Assert.True(mesh.CompleteMeshBuild(target));
        mesh.MarkUploaded();

        var bright = new TerrainLightSection(new TerrainLightChannels(0, 15));
        var unusedChanged = first.WithSection(7, bright);
        Publish(unusedChanged);
        Assert.True(mesh.HasCurrentUploadedGeometry(target));
        Publish(unusedChanged.WithSection(0, new TerrainLightSection(default)));
        Assert.True(mesh.HasCurrentUploadedGeometry(target));

        var sampledChanged = unusedChanged.WithSection(0, bright);
        Publish(sampledChanged);
        Assert.False(mesh.HasCurrentUploadedGeometry(target));
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(target));
        Assert.True(mesh.TryReuseSliceGeometry(0, out var inputs));
        Assert.NotEqual(default, inputs.Lighting);
        mesh.CompleteMeshSlice();

        var dark = new TerrainLightSection(default);
        Publish(sampledChanged.WithSection(7, dark));
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(target));
        Assert.Equal(1, mesh.CompletedMeshSlices);

        Publish(sampledChanged.WithSection(0, dark));
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(target));
        Assert.Equal(0, mesh.CompletedMeshSlices);

        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(target));
        mesh.MarkUploaded();
        neighbor.SetCellValueFast(8, 50, 8, Terrain.MakeBlockValue(1));
        neighbor.NetworkContentVersion++;
        Assert.True(mesh.HasCurrentUploadedGeometry(target));

        neighbor.SetCellValueFast(0, 50, 8, Terrain.MakeBlockValue(1));
        neighbor.NetworkContentVersion++;
        Assert.False(mesh.HasCurrentUploadedGeometry(target));
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(target));
        Assert.False(mesh.TryReuseSliceGeometry(0, out _));

        void Publish(TerrainLightSnapshot snapshot)
        {
            input = terrain.Lighting.CaptureInput(neighbor);
            Assert.True(terrain.Lighting.TryPublish(neighbor, input, snapshot));
            Assert.True(neighbor.DerivedRevisions.PublishLight(input.Request));
        }
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
        Assert.True(chunk.DerivedRevisions.PublishLight(chunk.DerivedRevisions.Capture()));
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
        TerrainMeshTestData.CompleteSlices(presentation);
        if (contentChanged) { chunk.NetworkContentVersion = 2; }
        else { presentation.RequestMeshRebuild(); }

        Assert.False(presentation.CompleteMeshBuild(chunk));
        Assert.False(presentation.NewGeometryData);
        Assert.Equal(TerrainMeshState.Building, presentation.PrepareMeshUpdate(chunk));
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
        TerrainMeshTestData.CompleteSlices(presentation);
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
        Assert.Equal(TerrainChunk.SlicesCount, presentations.Get(chunk).generatedSliceInputs.Length);
    }

    [Fact]
    public void SliceCursorPreservesTraversalAndOnlyPublishesTheWholeMesh()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.NetworkContentVersion = 1;
        TerrainMeshTestData.PrepareLighting(chunk);
        var mesh = presentations.Get(chunk);
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(chunk));
        var visited = new List<int>();
        while (mesh.CompletedMeshSlices < TerrainChunk.SlicesCount)
        {
            Assert.False(mesh.CompleteMeshBuild(chunk));
            Assert.False(mesh.NewGeometryData);
            visited.Add(mesh.NextMeshSlice);
            mesh.CompleteMeshSlice();
        }

        Assert.Equal(new[] { 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15 }, visited);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        Assert.Equal(TerrainMeshState.Ready, mesh.WorkerMeshState);
        Assert.True(mesh.NewGeometryData);
        Assert.Throws<InvalidOperationException>(mesh.CompleteMeshSlice);
    }

    [Fact]
    public void ChangedInputsRestartTheSliceCursorBeforeAnotherUnitRuns()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.NetworkContentVersion = 1;
        TerrainMeshTestData.PrepareLighting(chunk);
        var mesh = presentations.Get(chunk);
        mesh.PrepareMeshUpdate(chunk);
        mesh.CompleteMeshSlice();
        mesh.CompleteMeshSlice();
        Assert.Equal(2, mesh.CompletedMeshSlices);
        chunk.NetworkContentVersion++;
        Assert.Equal(TerrainMeshState.Building, mesh.PrepareMeshUpdate(chunk));
        Assert.Equal(0, mesh.CompletedMeshSlices);
        Assert.Equal(0, mesh.NextMeshSlice);
        Assert.False(mesh.CompleteMeshBuild(chunk));
    }

    [Fact]
    public void MaintenanceDoesNotStartAnotherCoalescingWaitInsideTheFirstSlice()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var mesh = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, mesh);
        mesh.MarkUploaded();
        mesh.RequestMeshRebuild();
        mesh.PrepareMeshUpdate(chunk);
        mesh.ActiveSlice = new TerrainChunkPresentation.SliceWork(0, new TerrainSliceInputs(123, 0)) { Column = 1 };
        Assert.Equal(0, mesh.CompletedMeshSlices);
        Assert.False(mesh.ShouldDeferMaintenance(chunk, 100, out _));
    }
}
