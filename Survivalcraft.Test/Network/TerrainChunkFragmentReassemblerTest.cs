using Engine.Core;
using Game.Network.Serialization;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class TerrainChunkFragmentReassemblerTest
{
    [Fact]
    public void ReassemblesOutOfOrderFragmentsAndIgnoresDuplicates()
    {
        var allocation = new ChunkAllocationId(new Point2(2, 3), 4);
        var encoded = new EncodedTerrainChunk(allocation.Coords, 5, Enumerable.Range(0, 2500)
            .Select(value => (byte)value).ToArray());
        var fragments = EncodedTerrainChunkFragmenter.Split(encoded, allocation).Reverse().ToArray();
        var reassembler = new TerrainChunkFragmentReassembler();

        Assert.False(reassembler.Add(fragments[0], out _));
        Assert.False(reassembler.Add(fragments[0], out _));
        EncodedTerrainChunk? completed = null;
        foreach (var fragment in fragments.Skip(1))
        {
            if (reassembler.Add(fragment, out var result))
            {
                completed = result;
            }
        }

        Assert.NotNull(completed);
        Assert.Equal(encoded.Payload, completed.Payload);
        Assert.False(reassembler.Add(fragments[0], out _));
        Assert.Equal(0, reassembler.PendingCount);
    }

    [Fact]
    public void RepeatedTransferFillsFragmentsMissingFromFirstAttempt()
    {
        var allocation = new ChunkAllocationId(new Point2(4, 5), 6);
        var encoded = new EncodedTerrainChunk(allocation.Coords, 7, new byte[2400]);
        var fragments = EncodedTerrainChunkFragmenter.Split(encoded, allocation).ToArray();
        var reassembler = new TerrainChunkFragmentReassembler();

        Assert.False(reassembler.Add(fragments[0], out _));
        Assert.False(reassembler.Add(fragments[2], out _));
        Assert.True(reassembler.Add(fragments[1], out var completed));
        Assert.Equal(encoded.Payload, completed.Payload);
    }

    [Fact]
    public void NewAllocationDiscardsPartialOldTransfer()
    {
        var coords = new Point2(8, 9);
        var oldAllocation = new ChunkAllocationId(coords, 1);
        var newAllocation = new ChunkAllocationId(coords, 2);
        var fragment = EncodedTerrainChunkFragmenter.Split(
            new EncodedTerrainChunk(coords, 3, new byte[1800]), oldAllocation).First();
        var reassembler = new TerrainChunkFragmentReassembler();

        Assert.False(reassembler.Add(fragment, out _));
        Assert.Equal(1, reassembler.DiscardOtherGenerations([newAllocation]));
        Assert.Equal(0, reassembler.PendingCount);
    }

    [Fact]
    public void DuplicateAllocationEntriesDoNotInterruptRequestCleanup()
    {
        var coords = new Point2(4, 33);
        var allocation = new ChunkAllocationId(coords, 2);
        var reassembler = new TerrainChunkFragmentReassembler();

        Assert.Equal(0, reassembler.DiscardOtherGenerations([allocation, allocation]));
    }

    [Fact]
    public void MissingFragmentRequestContainsOnlyUnreceivedIndices()
    {
        var allocation = new ChunkAllocationId(new Point2(6, 7), 8);
        var encoded = new EncodedTerrainChunk(allocation.Coords, 9, new byte[3200]);
        var fragments = EncodedTerrainChunkFragmenter.Split(encoded, allocation).ToArray();
        var reassembler = new TerrainChunkFragmentReassembler();

        Assert.False(reassembler.TryCreateMissingFragmentRequest(allocation, out _));
        Assert.False(reassembler.Add(fragments[0], out _));
        Assert.False(reassembler.Add(fragments[2], out _));

        Assert.True(reassembler.TryCreateMissingFragmentRequest(allocation, out var request));
        Assert.Equal(allocation, request.Allocation);
        Assert.Equal(encoded.ContentVersion, request.ContentVersion);
        Assert.Equal(fragments.Length, request.FragmentCount);
        Assert.Equal([1, 3], request.MissingFragmentIndices);
    }

    [Fact]
    public void MissingFragmentRequestDoesNotCrossAllocationGeneration()
    {
        var coords = new Point2(6, 7);
        var oldAllocation = new ChunkAllocationId(coords, 8);
        var newAllocation = new ChunkAllocationId(coords, 9);
        var fragment = EncodedTerrainChunkFragmenter.Split(
            new EncodedTerrainChunk(coords, 10, new byte[1800]),
            oldAllocation).First();
        var reassembler = new TerrainChunkFragmentReassembler();
        reassembler.Add(fragment, out _);

        Assert.False(reassembler.TryCreateMissingFragmentRequest(newAllocation, out _));
    }
}
