using Engine.Core.Mathematics;

using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class TerrainCodecTest
{
    public static TheoryData<string, SubsystemTerrainPackage> CapturedMessages => new()
    {
        {
            "terrain-request",
            new TerrainChunksRequestPackage
            {
                ChunkRequests = [new ChunkContentRequest(new ChunkAllocationId(new Point2(-2, 3), 17), 32)]
            }
        },
        {
            "terrain-fragment",
            new TerrainFragmentPackage
            {
                ChunkFragment = new EncodedTerrainChunkFragment(
                    new ChunkAllocationId(new Point2(-2, 3), 17),
                    32,
                    4096,
                    1,
                    11,
                    Enumerable.Range(0, 400).Select(i => (byte)i).ToArray()
                )
            }
        },
        {
            "terrain-delta",
            new TerrainCellDeltaPackage
                { CellDelta = new TerrainCellDelta(new Point3(1, 80, -2), 0, 32, 33) }
        }
    };

    [Theory]
    [MemberData(nameof(CapturedMessages))]
    public void TerrainMessagesRoundTripTypedPayloads(string name, SubsystemTerrainPackage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()), name);
        var encoded = ProtocolMessageCodec.Encode(message);
        var restored = Assert.IsAssignableFrom<SubsystemTerrainPackage>(ProtocolMessageCodec.Decode(encoded));
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(encoded, ProtocolMessageCodec.Encode(restored));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(1)]
    public void InvalidOrTruncatedRequestCountsAreRejected(int count)
    {
        var malformed = WireTestData.ReplaceField(
            new TerrainChunksRequestPackage(),
            0,
            count < 0 ? [0xff] : [0xdd, 0x7f, 0xff, 0xff, 0xff]
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }

    [Fact]
    public void FragmentPayloadRetainsItsWireSizeLimit()
    {
        var fragment = new TerrainFragmentPackage
            { ChunkFragment = new EncodedTerrainChunkFragment(default, 0, 65536, 0, 1, new byte[65536]) };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(fragment));
        var encoded = ProtocolPayloadCodec.Encode<IProtocolMessage>(fragment);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(encoded));
    }

    [Fact]
    public void FragmentRetryBitmapRoundTripsRequestedIndices()
    {
        var request = new TerrainChunkFragmentRequest(new ChunkAllocationId(new Point2(2, 3), 4), 5, 17, [0, 8, 16]);
        var message = SubsystemTerrainPackage.CreateFragmentRequest([request]);
        var writer = ProtocolMessageCodec.Encode(message);
        var reader = writer;
        var restored = Assert.IsType<TerrainFragmentsRequestPackage>(ProtocolMessageCodec.Decode(reader));
        var item = Assert.Single(restored.FragmentRequests);
        Assert.Equal(request.Allocation, item.Allocation);
        Assert.Equal(request.ContentVersion, item.ContentVersion);
        Assert.Equal(request.FragmentCount, item.FragmentCount);
        Assert.Equal(request.MissingFragmentIndices, item.MissingFragmentIndices);
    }
}
