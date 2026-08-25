using System.Diagnostics;

using Engine.Core;

using Game.Network;
using Game.Network.Serialization;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class NetworkChunkEncoderTest
{
    [Fact]
    public void CompletedSnapshotIsCommittedToVersionedCache()
    {
        var snapshot = Snapshot(new Point2(2, -3), 3, 7);
        var cache = new NetworkChunkCache();
        using var encoder = new NetworkChunkEncoder();

        Assert.True(encoder.TrySchedule(snapshot));
        var encoded = Assert.Single(WaitForCompletion(encoder, cache));

        Assert.True(cache.TryGet(snapshot.Coords, snapshot.ContentVersion, out var cached));
        Assert.Same(encoded, cached);
        Assert.Equal(7, Terrain.ExtractContents(
            NetworkChunkCodec.Decode(snapshot.Coords, encoded.Payload).GetCellValueFast(1, 20, 3)));
        Assert.Equal(0, encoder.OutstandingCount);
    }

    [Fact]
    public void ImmutableSnapshotIsUnaffectedByLaterAuthorityChanges()
    {
        var chunk = new TerrainChunk(null!, 0, 0);
        chunk.SetCellValueFast(1, 20, 3, Terrain.MakeBlockValue(7));
        var snapshot = new AuthorityChunkSnapshot(
            chunk.Coords,
            1,
            chunk.Cells.ToArray(),
            chunk.Shafts.ToArray());
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = new NetworkChunkEncoder(1, value =>
        {
            encodeStarted.Set();
            Assert.True(allowEncode.Wait(TimeSpan.FromSeconds(5)));
            return NetworkChunkCodec.Encode(value);
        });

        Assert.True(encoder.TrySchedule(snapshot));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        chunk.SetCellValueFast(1, 20, 3, Terrain.MakeBlockValue(9));
        allowEncode.Set();

        var encoded = Assert.Single(WaitForCompletion(encoder, new NetworkChunkCache()));
        Assert.Equal(7, Terrain.ExtractContents(
            NetworkChunkCodec.Decode(snapshot.Coords, encoded.Payload).GetCellValueFast(1, 20, 3)));
    }

    [Fact]
    public void BoundedQueueRejectsWorkBeyondOutstandingLimit()
    {
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = BlockingEncoder(2, encodeStarted, allowEncode);

        Assert.True(encoder.TrySchedule(Snapshot(new Point2(0, 0), 1)));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(encoder.TrySchedule(Snapshot(new Point2(1, 0), 1)));
        Assert.False(encoder.TrySchedule(Snapshot(new Point2(2, 0), 1)));
        Assert.Equal(2, encoder.OutstandingCount);
        allowEncode.Set();
    }

    [Fact]
    public void DefaultQueueAcceptsEntireConfiguredSendWindow()
    {
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = BlockingEncoder(
            NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
            encodeStarted,
            allowEncode);

        Assert.True(encoder.TrySchedule(Snapshot(new Point2(0, 0), 1)));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        for (var i = 1; i < NetworkTerrainPolicy.DefaultServerChunkCountSendPer; i++)
        {
            Assert.True(encoder.TrySchedule(Snapshot(new Point2(i, 0), 1)));
        }

        Assert.False(encoder.TrySchedule(Snapshot(
            new Point2(NetworkTerrainPolicy.DefaultServerChunkCountSendPer, 0),
            1)));
        allowEncode.Set();
    }

    [Fact]
    public void DuplicateDescriptorSharesOneOutstandingEncoding()
    {
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = BlockingEncoder(1, encodeStarted, allowEncode);
        var snapshot = Snapshot(new Point2(0, 0), 5);

        Assert.True(encoder.TrySchedule(snapshot));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(encoder.IsScheduled(new AuthorityChunkDescriptor(
            snapshot.Coords,
            snapshot.ContentVersion)));
        Assert.True(encoder.TrySchedule(Snapshot(snapshot.Coords, snapshot.ContentVersion)));
        Assert.Equal(1, encoder.OutstandingCount);
        allowEncode.Set();
    }

    private static NetworkChunkEncoder BlockingEncoder(
        int capacity,
        ManualResetEventSlim started,
        ManualResetEventSlim allow) => new(capacity, snapshot =>
    {
        started.Set();
        Assert.True(allow.Wait(TimeSpan.FromSeconds(5)));
        return NetworkChunkCodec.Encode(snapshot);
    });

    private static AuthorityChunkSnapshot Snapshot(Point2 coords, long version, int contents = 0)
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[TerrainChunk.CalculateCellIndex(1, 20, 3)] = Terrain.MakeBlockValue(contents);
        return new AuthorityChunkSnapshot(coords, version, cells, new long[AuthorityChunkSnapshot.ShaftCount]);
    }

    private static IReadOnlyList<EncodedTerrainChunk> WaitForCompletion(
        NetworkChunkEncoder encoder,
        NetworkChunkCache cache)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var completed = encoder.DrainCompleted(cache);
            if (completed.Count > 0)
            {
                return completed;
            }

            Thread.Sleep(5);
        }

        throw new TimeoutException("Timed out waiting for background chunk encoding.");
    }
}
