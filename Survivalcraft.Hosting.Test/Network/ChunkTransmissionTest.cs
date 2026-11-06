using Engine.Core.Mathematics;

using Game.Network.Chunks;
using Game.Protocol.Terrain;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class ChunkTransmissionTest
{
    [Fact]
    public void OversizedChunksMakeBoundedProgressAndResumeWithoutResendingThePrefix()
    {
        var encoded = new EncodedTerrainChunk(
            new Point2(0, 0),
            1,
            Enumerable.Range(0, 2000).Select(value => (byte)value).ToArray()
        );
        var transmission = new ChunkTransmission(encoded, new ChunkAllocationId(encoded.Coords, 1));
        transmission.RequestAll();
        var budget = new ChunkSendBudget();
        var sent = new List<EncodedTerrainChunkFragment>();
        for (var frame = 0; frame < 6; frame++)
        {
            budget.Advance(frame, 1, 352);
            Assert.True(transmission.TrySend(budget, sent.Add, out _, out _));
            Assert.False(transmission.TrySend(budget, sent.Add, out _, out _));
        }

        Assert.True(transmission.IsComplete);
        Assert.Equal(Enumerable.Range(0, 6), sent.Select(fragment => (int)fragment.FragmentIndex));
        Assert.Equal(encoded.Payload, sent.SelectMany(fragment => fragment.Payload));
    }

    [Fact]
    public void MissingBitmapMergesWithFutureFragmentsAndOnlyLostSentFragmentsCountAsRetries()
    {
        var encoded = new EncodedTerrainChunk(new Point2(0, 0), 1, new byte[1000]);
        var allocation = new ChunkAllocationId(encoded.Coords, 1);
        var transmission = new ChunkTransmission(encoded, allocation);
        transmission.RequestAll();
        var budget = new ChunkSendBudget();
        budget.Advance(0, 100, 10000);
        Assert.True(transmission.TrySend(budget, _ => { }, out var retry, out _));
        Assert.False(retry);
        Assert.True(transmission.RequestMissing(new TerrainChunkFragmentRequest(allocation, 1, 3, [0, 1, 2, 2])));
        budget.Advance(1, 100, 10000);
        var indices = new List<ushort>();
        var retried = 0;
        while (transmission.TrySend(budget, fragment => indices.Add(fragment.FragmentIndex), out retry, out _))
        {
            if (retry) { retried++; }
        }

        Assert.Equal<ushort>([0, 1, 2], indices);
        Assert.Equal(1, retried);
        Assert.True(transmission.IsComplete);
    }

    [Fact]
    public void InvalidRepairMetadataDoesNotMutateThePlan()
    {
        var encoded = new EncodedTerrainChunk(new Point2(0, 0), 1, new byte[1000]);
        var allocation = new ChunkAllocationId(encoded.Coords, 1);
        var transmission = new ChunkTransmission(encoded, allocation);
        Assert.False(transmission.RequestMissing(new TerrainChunkFragmentRequest(allocation, 2, 3, [0])));
        Assert.False(transmission.RequestMissing(new TerrainChunkFragmentRequest(allocation, 1, 2, [0])));
        Assert.False(transmission.RequestMissing(new TerrainChunkFragmentRequest(allocation, 1, 3, [3])));
        Assert.True(transmission.IsComplete);
    }
}
