using System.Diagnostics;

using Game.Network;
using Game.Network.Serialization;
using Game.Terrains;

namespace Survivalcraft.Test.Network;

public sealed class NetworkChunkEncoderTest
{
    [Fact]
    public void CompletedSnapshotIsCommittedToCache()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, -3);
        chunk.SetCellValueFast(1, 20, 3, Terrain.MakeBlockValue(7));
        var cache = new NetworkChunkCache();
        using var encoder = new NetworkChunkEncoder();

        Assert.True(encoder.TrySchedule(chunk));
        var completed = WaitForCompletion(encoder, terrain, cache);

        Assert.Same(chunk, Assert.Single(completed));
        Assert.True(cache.TryGet(chunk, out var encoded));
        var decoded = NetworkChunkCodec.Decode(chunk.Coords, encoded.Payload);
        Assert.Equal(
            Terrain.ReplaceLight(chunk.GetCellValueFast(1, 20, 3), 0),
            decoded.GetCellValueFast(1, 20, 3));
        Assert.Equal(0, encoder.OutstandingCount);
    }

    [Fact]
    public void ChangedChunkRejectsStaleBackgroundResult()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var cache = new NetworkChunkCache();
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = new NetworkChunkEncoder(1, snapshot =>
        {
            encodeStarted.Set();
            Assert.True(allowEncode.Wait(TimeSpan.FromSeconds(5)));
            return NetworkChunkCodec.Encode(snapshot);
        });

        Assert.True(encoder.TrySchedule(chunk));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        chunk.SetCellValueFast(0, 0, 0, Terrain.MakeBlockValue(9));
        allowEncode.Set();

        var completed = WaitUntilDrained(encoder, terrain, cache);
        Assert.Empty(completed);
        Assert.False(cache.TryGet(chunk, out _));
        Assert.Equal(0, encoder.OutstandingCount);
    }

    [Fact]
    public void BoundedQueueRejectsWorkBeyondOutstandingLimit()
    {
        using var terrain = new Terrain();
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = new NetworkChunkEncoder(2, snapshot =>
        {
            encodeStarted.Set();
            Assert.True(allowEncode.Wait(TimeSpan.FromSeconds(5)));
            return NetworkChunkCodec.Encode(snapshot);
        });

        Assert.True(encoder.TrySchedule(terrain.AllocateChunk(0, 0)));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(encoder.TrySchedule(terrain.AllocateChunk(1, 0)));
        Assert.False(encoder.TrySchedule(terrain.AllocateChunk(2, 0)));
        Assert.Equal(2, encoder.OutstandingCount);

        allowEncode.Set();
    }

    [Fact]
    public void DefaultQueueAcceptsEntireConfiguredSendWindow()
    {
        using var terrain = new Terrain();
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = new NetworkChunkEncoder(
            NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
            snapshot =>
            {
                encodeStarted.Set();
                Assert.True(allowEncode.Wait(TimeSpan.FromSeconds(5)));
                return NetworkChunkCodec.Encode(snapshot);
            });

        Assert.True(encoder.TrySchedule(terrain.AllocateChunk(0, 0)));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        for (var i = 1; i < NetworkTerrainPolicy.DefaultServerChunkCountSendPer; i++)
        {
            Assert.True(encoder.TrySchedule(terrain.AllocateChunk(i, 0)));
        }

        Assert.False(encoder.TrySchedule(terrain.AllocateChunk(
            NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
            0)));
        Assert.Equal(NetworkTerrainPolicy.DefaultServerChunkCountSendPer, encoder.OutstandingCount);

        allowEncode.Set();
    }

    [Fact]
    public void DuplicateRequestSharesOneOutstandingEncoding()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var encodeStarted = new ManualResetEventSlim();
        using var allowEncode = new ManualResetEventSlim();
        using var encoder = new NetworkChunkEncoder(1, snapshot =>
        {
            encodeStarted.Set();
            Assert.True(allowEncode.Wait(TimeSpan.FromSeconds(5)));
            return NetworkChunkCodec.Encode(snapshot);
        });

        Assert.True(encoder.TrySchedule(chunk));
        Assert.True(encodeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(encoder.TrySchedule(chunk));
        Assert.Equal(1, encoder.OutstandingCount);

        allowEncode.Set();
    }

    private static IReadOnlyList<TerrainChunk> WaitForCompletion(
        NetworkChunkEncoder encoder,
        Terrain terrain,
        NetworkChunkCache cache)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var completed = encoder.DrainCompleted(terrain, cache);
            if (completed.Count > 0)
            {
                return completed;
            }

            Thread.Sleep(5);
        }

        throw new TimeoutException("Timed out waiting for background chunk encoding.");
    }

    private static IReadOnlyList<TerrainChunk> WaitUntilDrained(
        NetworkChunkEncoder encoder,
        Terrain terrain,
        NetworkChunkCache cache)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var completed = encoder.DrainCompleted(terrain, cache);
            if (encoder.OutstandingCount == 0)
            {
                return completed;
            }

            Thread.Sleep(5);
        }

        throw new TimeoutException("Timed out draining background chunk encoding.");
    }
}
