using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class TerrainDeltaBackpressureTest
{
    [Fact]
    public void DrainIsBoundedAndSharesTheBatchAcrossChunksWithoutDroppingTheRemainder()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        var allocations = Enumerable.Range(0, 8).Select(x => new ChunkAllocationId(new Point2(x, 0), 1)).ToArray();
        content.Request(allocations.Select(allocation => new ChunkContentRequest(allocation)).ToArray());
        foreach (var allocation in allocations)
        {
            for (var version = 1; version <= 256; version++) { content.Receive(Delta(allocation.Coords, version)); }
        }

        var first = new List<TerrainCellDelta>();
        Assert.Equal(ClientChunkContentTransport.MaximumDeltasPerDrain, content.DrainDeltas(first));
        Assert.Equal(8, first.Select(delta => Terrain.ToChunk(delta.Cell.X, delta.Cell.Z)).Distinct().Count());
        Assert.All(
            allocations,
            allocation => Assert.Equal(
                128,
                first.Count(delta => Terrain.ToChunk(delta.Cell.X, delta.Cell.Z) == allocation.Coords)
            )
        );
        Assert.Equal(1024, content.PendingDeltaCount);
        var second = new List<TerrainCellDelta>();
        Assert.Equal(1024, content.DrainDeltas(second));
        Assert.Equal(0, content.PendingDeltaCount);
        Assert.All(
            allocations,
            allocation => Assert.Equal(
                Enumerable.Range(1, 256).Select(value => (long)value),
                first.Concat(second).Where(delta => Terrain.ToChunk(delta.Cell.X, delta.Cell.Z) == allocation.Coords)
                    .Select(delta => delta.BaseContentVersion)
            )
        );
    }

    [Fact]
    public void IngressOverflowKeepsTheNewestVersionFloorUntilACompleteSnapshotCatchesUp()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var allocation = new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration);
        content.Request([new ChunkContentRequest(allocation)]);
        for (var version = 1; version <= 1000; version++) { content.Receive(Delta(chunk.Coords, version)); }

        Assert.Equal(0, content.PendingDeltaCount);
        Assert.Equal(1, content.PendingResyncCount);
        var requests = new List<ChunkContentResync>();
        Assert.Equal(1, content.DrainResyncRequests(requests));
        var request = Assert.Single(requests);
        Assert.Equal(1001, request.RequiredContentVersion);
        var coordinator = new ClientTerrainDeltaCoordinator(
            terrain,
            _ => throw new InvalidOperationException("Lost chain applied.")
        );
        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, coordinator.RequireSnapshot(request));
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 500;
        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, coordinator.OnContentInstalled(chunk));
        Assert.True(chunk.HasContents);
        Assert.True(chunk.NeedsContentSnapshot);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 1001;
        Assert.Equal(TerrainDeltaApplyResult.Ignored, coordinator.OnContentInstalled(chunk));
        Assert.True(chunk.HasContents);
    }

    [Fact]
    public void IngressAndCoordinatorHaveGlobalBoundsAcrossDifferentChunks()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var terrain = new Terrain();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        var chunks = Enumerable.Range(0, 33).Select(x => terrain.AllocateChunk(x, 0)).ToArray();
        content.Request(
            chunks.Select(chunk =>
                new ChunkContentRequest(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration))
            ).ToArray()
        );
        foreach (var chunk in chunks.Take(32))
        {
            for (var version = 1; version <= 256; version++)
            {
                var delta = Delta(chunk.Coords, version);
                content.Receive(delta);
                coordinator.Receive(delta);
            }
        }

        Assert.Equal(ClientChunkContentTransport.MaximumQueuedDeltas, content.PendingDeltaCount);
        Assert.Equal(ClientTerrainDeltaCoordinator.MaximumPendingDeltas, coordinator.PendingDeltaCount);
        content.Receive(Delta(chunks[^1].Coords, 1));
        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, coordinator.Receive(Delta(chunks[^1].Coords, 1)));
        Assert.Equal(ClientChunkContentTransport.MaximumQueuedDeltas, content.PendingDeltaCount);
        Assert.Equal(ClientTerrainDeltaCoordinator.MaximumPendingDeltas, coordinator.PendingDeltaCount);
        Assert.Equal(1, content.PendingResyncCount);
        content.Discard(new ChunkAllocationId(chunks[0].Coords, chunks[0].AllocationGeneration));
        coordinator.Discard(chunks[0].Coords);
        Assert.Equal(ClientChunkContentTransport.MaximumQueuedDeltas - 256, content.PendingDeltaCount);
        Assert.Equal(ClientTerrainDeltaCoordinator.MaximumPendingDeltas - 256, coordinator.PendingDeltaCount);
        node.Runtime.Close();
        Assert.Equal(0, content.PendingDeltaCount);
        Assert.Equal(0, content.PendingResyncCount);
    }

    [Fact]
    public void DuplicateVersionsDoNotSpendCapacityAndAllocationReleaseDropsTheirWork()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        content.Receive(Delta(allocation.Coords, 1));
        Assert.Equal(0, content.PendingDeltaCount);
        content.Request([new ChunkContentRequest(allocation)]);
        for (var count = 0; count < 1000; count++) { content.Receive(Delta(allocation.Coords, 1)); }

        Assert.Equal(1, content.PendingDeltaCount);
        content.Discard(allocation);
        var replacement = allocation with { Generation = 2 };
        content.Request([new ChunkContentRequest(replacement)]);
        Assert.Equal(0, content.DrainDeltas(new List<TerrainCellDelta>()));
        Assert.Equal(0, content.DrainResyncRequests(new List<ChunkContentResync>()));
    }


    private static TerrainCellDelta Delta(Point2 coords, long version) =>
        new(new Point3(coords.X * 16 + 1, 64, coords.Y * 16 + 1), 7, version, version + 1);
}
