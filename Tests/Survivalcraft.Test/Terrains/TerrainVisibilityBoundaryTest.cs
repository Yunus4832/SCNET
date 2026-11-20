using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainVisibilityBoundaryTest
{
    [Fact]
    public void CompleteCoverageStartsAtTargetWithoutAnInitialFade()
    {
        var boundary = new TerrainVisibilityBoundary();
        boundary.Update(new Vector2(8f, 8f), 128f, 0f, _ => true);
        Assert.Equal(128f, boundary.SafeDistance);
        Assert.Equal(128f, boundary.DisplayDistance);
    }

    [Theory]
    [InlineData(8f, 8f, 2, 0, 24f)]
    [InlineData(-8f, -8f, -3, -1, 24f)]
    [InlineData(0f, 0f, 1, 0, 16f)]
    public void MissingCoordinatesLimitCoverageAtTheNearestRectangle(
        float x,
        float z,
        int missingX,
        int missingZ,
        float expected
    )
    {
        var missing = new Point2(missingX, missingZ);
        var distance = TerrainVisibilityBoundary.CalculateSafeDistance(
            new Vector2(x, z),
            128f,
            coords => coords != missing
        );
        Assert.Equal(expected - TerrainVisibilityBoundary.coveragePadding, distance, 4);
    }

    [Fact]
    public void DiagonalAndOutsideDiskChunksUseRectangleDistance()
    {
        var center = new Vector2(8f, 8f);
        var missing = new Point2(2, 2);
        var distance = TerrainVisibilityBoundary.CalculateSafeDistance(center, 64f, coords => coords != missing);
        Assert.Equal(MathF.Sqrt(24f * 24f * 2f) - TerrainVisibilityBoundary.coveragePadding, distance, 4);
        Assert.Equal(16f, TerrainVisibilityBoundary.CalculateSafeDistance(center, 16f, coords => coords != missing));
    }

    [Fact]
    public void MissingCameraChunkAndZeroDistanceProduceFullyHiddenCoverage()
    {
        Assert.Equal(0f, TerrainVisibilityBoundary.CalculateSafeDistance(new Vector2(8f), 128f, _ => false));
        Assert.Equal(0f, TerrainVisibilityBoundary.CalculateSafeDistance(Vector2.Zero, 0f, _ => true));
    }

    [Fact]
    public void ExpansionIsSmoothAndNewHolesImmediatelyConstrainDisplay()
    {
        var boundary = new TerrainVisibilityBoundary();
        var center = new Vector2(8f, 8f);
        boundary.Update(center, 128f, 0f, coords => coords != new Point2(2, 0));
        var initial = boundary.DisplayDistance;
        boundary.Update(center, 128f, 1f / 60f, _ => true);
        Assert.InRange(boundary.DisplayDistance, initial + 0.01f, 127.99f);
        boundary.Update(center, 128f, 1f / 60f, coords => coords != new Point2(1, 0));
        Assert.Equal(8f - TerrainVisibilityBoundary.coveragePadding, boundary.DisplayDistance, 4);
        boundary.Update(center, 4f, 1f / 60f, _ => true);
        Assert.Equal(4f, boundary.DisplayDistance);
    }

    [Fact]
    public void SeparateViewsHaveIndependentCoverageAndExpansion()
    {
        var first = new TerrainVisibilityBoundary();
        var second = new TerrainVisibilityBoundary();
        first.Update(new Vector2(8f), 128f, 0f, _ => true);
        second.Update(new Vector2(-8f), 64f, 0f, _ => false);
        Assert.Equal(128f, first.DisplayDistance);
        Assert.Equal(0f, second.DisplayDistance);
        second.Update(new Vector2(-8f), 64f, 1f / 60f, _ => true);
        Assert.InRange(second.DisplayDistance, 0.01f, 63.99f);
        Assert.Equal(128f, first.DisplayDistance);
    }

    [Fact]
    public void TeleportInitializesFromCoverageAtTheNewCameraPosition()
    {
        var boundary = new TerrainVisibilityBoundary();
        boundary.Update(new Vector2(8f), 128f, 0f, _ => false);
        boundary.Update(new Vector2(1000f), 128f, 1f / 60f, _ => true);
        Assert.Equal(128f, boundary.DisplayDistance);
    }

    [Fact]
    public void ReadOnlyLookupAndDrawableCoverageRespectUploadedGeometryLifecycle()
    {
        using var terrain = new Terrain();
        using var store = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        Assert.False(store.TryGet(chunk, out var missing));
        Assert.Null(missing);
        var mesh = store.Get(chunk);
        Assert.True(store.TryGet(chunk, out var found));
        Assert.Same(mesh, found);
        Assert.False(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, mesh));
        TerrainMeshTestData.Complete(chunk, mesh);
        mesh.MarkUploaded();
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, mesh));
        chunk.SetCellValueFast(8, 64, 8, 3);
        chunk.DerivedRevisions.InvalidateHeightsAndLight();
        mesh.RequestMeshRebuild();
        Assert.False(mesh.HasCurrentUploadedGeometry(chunk));
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, mesh));
        var generation = chunk.AllocationGeneration;
        terrain.RenewChunkAllocation(chunk);
        Assert.False(store.TryGet(chunk, out _));
        store.RenewAllocation(chunk, generation);
        Assert.True(store.TryGet(chunk, out var retained));
        Assert.Same(mesh, retained);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, mesh));
        mesh.Dispose();
        Assert.False(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, mesh));
        store.Release(chunk);
        Assert.False(store.TryGet(chunk, out _));
    }
}
