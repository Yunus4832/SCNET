using Engine.Core.Mathematics;

using Game.Network.Chunks;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class TerrainChunkContentAuthorityTest
{
    [Fact]
    public void ReloadedServerChunkDoesNotDiscardDigDeltaOrImmediatelyRollBackPrediction()
    {
        using var server = new Terrain();
        using var client = new Terrain();
        var original = server.AllocateChunk(0, 0);
        original.SetCellValueFast(1, 64, 1, 7);
        for (var i = 0; i < 100; i++)
        {
            original.SetCellValueFast(2, 64, 1, i % 2 == 0 ? 7 : 12);
        }

        original.IsLoaded = true;
        original.WorkerState = TerrainChunkState.InvalidLight;
        var authority = new TerrainChunkContentAuthority(server);
        Assert.True(authority.TryGetSnapshot(original.Coords, out var initial));
        var cache = new NetworkChunkCache();
        var initialEncoding = cache.GetOrEncode(initial);

        var replica = client.AllocateChunk(0, 0);
        var snapshots = new ClientChunkContentCoordinator(client);
        var allocation = new ChunkAllocationId(replica.Coords, replica.AllocationGeneration);
        Assert.True(
            snapshots.TryInstall(
                new ClientChunkSnapshot(
                    allocation,
                    initial.ContentVersion,
                    initial.Cells.ToArray(),
                    initial.Shafts.ToArray()
                )
            )
        );

        server.FreeChunk(original);
        var reloaded = server.AllocateChunk(0, 0);
        // Restore through the same per-cell writes as TerrainSerializer24, with no generation history.
        for (var index = 0; index < initial.Cells.Length; index++)
        {
            reloaded.SetCellValueFast(index, initial.Cells.Span[index]);
        }

        reloaded.IsLoaded = true;
        reloaded.WorkerState = TerrainChunkState.InvalidLight;
        var baseVersion = reloaded.NetworkContentRevision + 1;
        var cell = new Point3(1, 64, 1);
        var prediction = new ClientTerrainPrediction(client, _ => { });
        var requestId = prediction.Begin(cell, 7, 0, 0);
        reloaded.SetCellValueFast(1, 64, 1, 0);
        var resultVersion = reloaded.NetworkContentRevision + 1;
        var deltas = new ClientTerrainDeltaCoordinator(
            client,
            delta => client.SetCellValueFast(delta.Cell.X, delta.Cell.Y, delta.Cell.Z, delta.Value)
        );
        var result = deltas.Receive(new TerrainCellDelta(cell, 0, baseVersion, resultVersion));
        prediction.Complete(requestId, cell, resultVersion);

        Assert.NotEqual(TerrainDeltaApplyResult.Ignored, result);
        Assert.True(prediction.IsPending(cell));
        Assert.True(resultVersion > initial.ContentVersion);
        Assert.Equal(0, Terrain.ExtractContents(client.GetCellValueForGeometry(1, 64, 1)));

        // Skipped reload revisions require a newer full baseline, not an ignored older delta.
        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, result);
        snapshots.ContentInstalled += chunk => deltas.OnContentInstalled(chunk);
        snapshots.ContentInstalled += prediction.OnContentInstalled;
        Assert.True(authority.TryGetSnapshot(reloaded.Coords, out var latest));
        var latestEncoding = cache.GetOrEncode(latest);
        Assert.NotSame(initialEncoding, latestEncoding);
        Assert.Equal(resultVersion, latestEncoding.ContentVersion);
        Assert.True(
            snapshots.TryInstall(
                new ClientChunkSnapshot(
                    allocation,
                    latest.ContentVersion,
                    latest.Cells.ToArray(),
                    latest.Shafts.ToArray()
                )
            )
        );
        Assert.False(prediction.IsPending(cell));
        Assert.Equal(0, Terrain.ExtractContents(client.GetCellValueFast(1, 64, 1)));

        // Subsequent edits use contiguous deltas again, without another reconnect or snapshot.
        reloaded.SetCellValueFast(2, 64, 1, 0);
        Assert.Equal(
            TerrainDeltaApplyResult.Applied,
            deltas.Receive(
                new TerrainCellDelta(new Point3(2, 64, 1), 0, resultVersion, reloaded.NetworkContentRevision + 1)
            )
        );
        Assert.Equal(0, Terrain.ExtractContents(client.GetCellValueFast(2, 64, 1)));
    }

    [Fact]
    public void AuthorityPublishesOnlyLoadedContentAndTracksContentChanges()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, 3);
        var authority = new TerrainChunkContentAuthority(terrain);

        Assert.False(authority.TryGetSnapshot(chunk.Coords, out _));

        chunk.IsLoaded = true;
        chunk.WorkerState = TerrainChunkState.InvalidLight;
        chunk.SetCellValueFast(0, 0, 0, 7);
        Assert.True(authority.TryGetDescriptor(chunk.Coords, out var descriptor));
        Assert.True(authority.TryGetSnapshot(chunk.Coords, out var first));
        Assert.Equal(first.ContentVersion, descriptor.ContentVersion);
        Assert.Equal(7, first.Cells.Span[0]);

        chunk.SetCellValueFast(0, 0, 0, 8);
        Assert.True(authority.TryGetSnapshot(chunk.Coords, out var second));
        Assert.True(second.ContentVersion > first.ContentVersion);
        Assert.Equal(8, second.Cells.Span[0]);
    }

    [Fact]
    public void AuthoritySnapshotDoesNotExposeMutableTerrainArrays()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, 3);
        chunk.IsLoaded = true;
        chunk.WorkerState = TerrainChunkState.InvalidLight;
        chunk.SetCellValueFast(0, 0, 0, 7);
        var authority = new TerrainChunkContentAuthority(terrain);

        Assert.True(authority.TryGetSnapshot(new Point2(2, 3), out var snapshot));
        chunk.SetCellValueFast(0, 0, 0, 9);

        Assert.Equal(7, snapshot.Cells.Span[0]);
    }
}
