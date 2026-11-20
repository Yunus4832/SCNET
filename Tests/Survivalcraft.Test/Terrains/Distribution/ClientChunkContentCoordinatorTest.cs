using Engine.Core.Mathematics;

using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains.Distribution;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientChunkContentCoordinatorTest
{
    [Fact]
    public void RequestedRefreshReplacesOnlyTheCurrentAllocationAndKeepsUploadedGeometryDuringRebuild()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var scope = new WorldCatalogScope(RuntimeSide.Client);
        var world = new SubsystemTerrain { Terrain = terrain };
        scope.Project.InitializeSubsystem(world);
        var content = new ClientChunkContentCoordinator(terrain);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.ClientChunkContents))!.SetValue(world, content);
        using var computations = new TerrainComputationScheduler(
            world,
            new TerrainLightingCalculator(
                terrain,
                scope.Catalog,
                true,
                candidate => candidate.HasContents && !candidate.NeedsContentValidation
            )
        );
        content.ContentInstalled += computations.ContentAvailable;
        content.ContentInstalled += chunk =>
        {
            new ClientChunkDerivationPipeline(presentations).Begin(chunk);
        };
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 10, 20)));


        presentations.Get(chunk).GeometryUploaded = true;
        TerrainMeshTestData.Complete(chunk, presentations.Get(chunk));
        var deltas = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        content.ContentInstalled += target => deltas.OnContentInstalled(target);
        Assert.Equal(
            TerrainDeltaApplyResult.ResyncRequired,
            deltas.RequireSnapshot(
                new ChunkContentResync(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration), 12)
            )
        );
        Assert.Equal(20, chunk.Cells[0]);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 10, 99)));
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 9, 99)));
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration + 1, 12, 99)));
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 11, 30)));
        Assert.True(chunk.NeedsContentSnapshot);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 12, 40)));
        Assert.False(chunk.NeedsContentSnapshot);
        Assert.True(chunk.HasContents);
        Assert.Equal(40, chunk.Cells[0]);
        Assert.False(chunk.HasCurrentHeightData);
        Assert.False(chunk.HasCurrentLight);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
        Assert.False(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 13, 50)));
        Assert.Equal(13, chunk.NetworkContentVersion);
        Assert.Equal(50, chunk.Cells[0]);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
    }

    private static ClientChunkSnapshot Snapshot(
        Point2 coords,
        ulong generation,
        long version,
        int value
    )
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = value;
        return new ClientChunkSnapshot(
            new ChunkAllocationId(coords, generation),
            version,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
    }
}
