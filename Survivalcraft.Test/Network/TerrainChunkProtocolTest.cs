using Engine.Core;

using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class TerrainChunkProtocolTest
{
    [Fact]
    public void RequestRoundTripPreservesAllocationGenerationAndKnownVersion()
    {
        var expected = new ChunkContentRequest(
            new ChunkAllocationId(new Point2(3, 4), 17),
            23);
        var clone = RoundTrip(new SubsystemTerrainPackage([expected]));

        Assert.Equal(expected, Assert.Single(clone.ChunkRequests));
    }

    [Fact]
    public void FragmentRoundTripPreservesTransferMetadataAndPayload()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(3, 4);
        chunk.SetCellValueFast(0, 0, 0, 12);
        var encoded = NetworkChunkCodec.Encode(chunk);
        var allocation = new ChunkAllocationId(chunk.Coords, 99);
        var fragment = Assert.Single(EncodedTerrainChunkFragmenter.Split(
            encoded,
            allocation,
            encoded.Payload.Length));
        var clone = RoundTrip(new SubsystemTerrainPackage(fragment));

        Assert.Equal(fragment.Allocation, clone.ChunkFragment.Allocation);
        Assert.Equal(fragment.ContentVersion, clone.ChunkFragment.ContentVersion);
        Assert.Equal(fragment.TotalLength, clone.ChunkFragment.TotalLength);
        Assert.Equal(fragment.FragmentIndex, clone.ChunkFragment.FragmentIndex);
        Assert.Equal(fragment.FragmentCount, clone.ChunkFragment.FragmentCount);
        Assert.Equal(fragment.Payload, clone.ChunkFragment.Payload);
    }

    [Fact]
    public void FailureRoundTripPreservesAllocationGeneration()
    {
        var expected = new ChunkAllocationId(new Point2(7, 8), 31);
        var clone = RoundTrip(new SubsystemTerrainPackage([expected], 0));

        Assert.Equal(expected, Assert.Single(clone.FailedChunkRequests));
    }

    [Fact]
    public void MissingFragmentRequestRoundTripPreservesIndices()
    {
        var expected = new TerrainChunkFragmentRequest(
            new ChunkAllocationId(new Point2(11, 12), 13),
            14,
            6,
            [1, 4, 5]);

        var clone = RoundTrip(SubsystemTerrainPackage.CreateFragmentRequest([expected]));
        var actual = Assert.Single(clone.FragmentRequests);

        Assert.Equal(expected.Allocation, actual.Allocation);
        Assert.Equal(expected.ContentVersion, actual.ContentVersion);
        Assert.Equal(expected.FragmentCount, actual.FragmentCount);
        Assert.Equal(expected.MissingFragmentIndices, actual.MissingFragmentIndices);
    }

    [Fact]
    public void CellDeltaRoundTripPreservesVersionTransition()
    {
        var expected = new TerrainCellDelta(new Point3(4, 5, 6), 7, 8, 9);

        var clone = RoundTrip(new SubsystemTerrainPackage(expected));

        Assert.Equal(expected, clone.CellDelta);
    }

    private static SubsystemTerrainPackage RoundTrip(SubsystemTerrainPackage package)
    {
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new SubsystemTerrainPackage();
        clone.ReadData(reader);
        return clone;
    }
}
