using Engine.Core;

using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class TerrainCodecTest
{
    public static TheoryData<string, SubsystemTerrainPackage> CapturedMessages => new()
    {
        { "terrain-request", new SubsystemTerrainPackage([new ChunkContentRequest(new ChunkAllocationId(new Point2(-2, 3), 17), 32)]) },
        { "terrain-fragment", new SubsystemTerrainPackage(new EncodedTerrainChunkFragment(new ChunkAllocationId(new Point2(-2, 3), 17), 32, 4096, 1, 11, Enumerable.Range(0, 400).Select(i => (byte)i).ToArray())) },
        { "terrain-delta", new SubsystemTerrainPackage(new TerrainCellDelta(new Point3(1, 80, -2), 0, 32, 33)) }
    };

    [Theory]
    [MemberData(nameof(CapturedMessages))]
    public void EncodingMatchesIndependentPreRefactorCapture(string name, SubsystemTerrainPackage message)
    {
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88);
        writer.Write(message.ID);
        ProtocolMessageCodec.Write(message, writer);
        var encoded = writer.Data(CompressionPolicy.None);
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".bin")), encoded);
        using var reader = new ProtocolReader(encoded);
        Assert.Equal(0x88, reader.ReadByte());
        var restored = Assert.IsType<SubsystemTerrainPackage>(ProtocolMessageCodec.Read(reader.ReadByte(), reader));
        Assert.Equal(message.Type, restored.Type);
        Assert.Equal(message.ChunkRequests, restored.ChunkRequests);
        Assert.Equal(message.CellDelta, restored.CellDelta);
        if (message.Type == SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment)
        {
            Assert.Equal(message.ChunkFragment.Payload, restored.ChunkFragment.Payload);
            Assert.Equal(message.ChunkFragment.Allocation, restored.ChunkFragment.Allocation);
            Assert.Equal(message.ChunkFragment.ContentVersion, restored.ChunkFragment.ContentVersion);
        }
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(1)]
    public void InvalidOrTruncatedRequestCountsAreRejected(int count)
    {
        using var writer = new ProtocolWriter();
        writer.Write((byte)SubsystemTerrainPackage.DataType.RequestSyncChunks);
        writer.Write(count);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.SubsystemTerrain, reader));
    }

    [Fact]
    public void FragmentRetryBitmapRoundTripsRequestedIndices()
    {
        var request = new TerrainChunkFragmentRequest(new ChunkAllocationId(new Point2(2, 3), 4), 5, 17, [0, 8, 16]);
        var message = SubsystemTerrainPackage.CreateFragmentRequest([request]);
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<SubsystemTerrainPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        var item = Assert.Single(restored.FragmentRequests);
        Assert.Equal(request.Allocation, item.Allocation);
        Assert.Equal(request.ContentVersion, item.ContentVersion);
        Assert.Equal(request.FragmentCount, item.FragmentCount);
        Assert.Equal(request.MissingFragmentIndices, item.MissingFragmentIndices);
    }
}
