using Engine.Core.Mathematics;

using Game.Network.Chunks;

namespace Survivalcraft.Test.Network;

public sealed class TerrainReassemblyLimitsTest
{
    [Fact]
    public void ADiscardedCompletionCannotEvictANewerCompletionWithTheSameVersion()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        var fragment = EncodedTerrainChunkFragmenter
            .Split(new EncodedTerrainChunk(allocation.Coords, 1, [0]), allocation).Single();
        var reassembler = new TerrainChunkFragmentReassembler();
        Assert.True(reassembler.Add(fragment, out _));
        reassembler.Discard(allocation);
        Assert.True(reassembler.Add(fragment, out _));
        for (var x = 1; x <= 4095; x++)
        {
            var next = new ChunkAllocationId(new Point2(x, 0), 1);
            Assert.True(reassembler.Add(fragment with { Allocation = next }, out _));
        }

        Assert.False(reassembler.Add(fragment, out _));
        Assert.Equal(0, reassembler.ReservedBytes);
    }

    [Fact]
    public void CountAndByteReservationsRejectAdditionalTransfersWithoutLeakingCredit()
    {
        var fragments = Enumerable.Range(0, 3).Select(x => EncodedTerrainChunkFragmenter.Split(
                new EncodedTerrainChunk(new Point2(x, 0), 1, new byte[1000]),
                new ChunkAllocationId(new Point2(x, 0), 1)
            ).First()
        ).ToArray();
        var countBound = new TerrainChunkFragmentReassembler(1);
        countBound.Add(fragments[0], out _);
        var reservation = countBound.ReservedBytes;
        Assert.False(countBound.Add(fragments[1], out _));
        Assert.Equal(1, countBound.PendingCount);
        Assert.Equal(reservation, countBound.ReservedBytes);
        countBound.Discard(fragments[0].Allocation);
        Assert.Equal(0, countBound.ReservedBytes);
        countBound.Add(fragments[1], out _);
        Assert.Equal(1, countBound.PendingCount);

        var byteBound = new TerrainChunkFragmentReassembler(3, reservation * 2 - 1);
        byteBound.Add(fragments[0], out _);
        byteBound.Add(fragments[1], out _);
        Assert.Equal(1, byteBound.PendingCount);
        Assert.Equal(reservation, byteBound.ReservedBytes);
        byteBound.Clear();
        Assert.Equal(0, byteBound.PendingCount);
        Assert.Equal(0, byteBound.ReservedBytes);
    }

    [Fact]
    public void OlderVersionsAndConflictingShapesCannotResetANewerAssembly()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        var fragments = EncodedTerrainChunkFragmenter.Split(
            new EncodedTerrainChunk(allocation.Coords, 2, new byte[1000]),
            allocation
        ).ToArray();
        var reassembler = new TerrainChunkFragmentReassembler();
        reassembler.Add(fragments[0], out _);
        var bytes = reassembler.ReservedBytes;
        Assert.False(reassembler.Add(fragments[0] with { ContentVersion = 1 }, out _));
        Assert.Throws<InvalidDataException>(() => reassembler.Add(fragments[0] with { TotalLength = 900 }, out _));
        Assert.Throws<InvalidDataException>(() => reassembler.Add(fragments[1] with { Payload = new byte[1] }, out _));
        Assert.Equal(bytes, reassembler.ReservedBytes);
        Assert.True(reassembler.TryCreateMissingFragmentRequest(allocation, out var missing));
        Assert.Equal(2, missing.ContentVersion);
        Assert.DoesNotContain((ushort)0, missing.MissingFragmentIndices);
        reassembler.Add(fragments[1], out _);
        Assert.True(reassembler.Add(fragments[2], out _));
        Assert.Equal(0, reassembler.ReservedBytes);
        Assert.False(reassembler.Add(fragments[0] with { ContentVersion = 1 }, out _));
    }

    [Fact]
    public void AcceptedFragmentBytesAreOwnedByTheAssembler()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        var bytes = Enumerable.Range(0, 1000).Select(value => (byte)value).ToArray();
        var fragments = EncodedTerrainChunkFragmenter
            .Split(new EncodedTerrainChunk(allocation.Coords, 1, bytes), allocation).ToArray();
        var reassembler = new TerrainChunkFragmentReassembler();
        reassembler.Add(fragments[0], out _);
        Array.Clear(fragments[0].Payload);
        reassembler.Add(fragments[1], out _);
        Assert.True(reassembler.Add(fragments[2], out var completed));
        Assert.Equal(bytes, completed.Payload);
    }
}
