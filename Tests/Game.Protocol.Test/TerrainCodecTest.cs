using Engine.Core.Mathematics;

using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class TerrainCodecTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void TerrainDeltaBatchHasAnExplicitBound(int count)
    {
        var delta = new TerrainCellDelta(new Point3(1, 65, 1), 4, 1, 2);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(
                new TerrainCellDeltaPackage
                {
                    CellDeltas = Enumerable.Repeat(delta, count).ToArray()
                }
            )
        );
    }

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
                { CellDeltas = [new TerrainCellDelta(new Point3(1, 80, -2), 0, 32, 33)] }
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
            new TerrainChunksRequestPackage
                { ChunkRequests = [new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1))] },
            0,
            count < 0 ? [0xff] : [0xdd, 0x7f, 0xff, 0xff, 0xff]
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void TerrainRequestCountIsBoundedForLogicalAndEncodedMessages(int count)
    {
        var request = new TerrainChunksRequestPackage
        {
            ChunkRequests = Enumerable.Range(0, count)
                .Select(x => new ChunkContentRequest(new ChunkAllocationId(new Point2(x, 0), 1))).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageValidation.Validate(request));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(request));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(ProtocolPayloadCodec.Encode<IProtocolMessage>(request))
        );
    }

    [Fact]
    public void MissingFragmentRequestsCannotUseRepeatedIndicesToExceedTheBitmapBound()
    {
        var package = new TerrainFragmentsRequestPackage
        {
            FragmentRequests =
            [
                new TerrainChunkFragmentRequest(
                    new ChunkAllocationId(new Point2(0, 0), 1),
                    1,
                    1,
                    new ushort[SubsystemTerrainPackage.MaximumRequestedFragmentCount + 1]
                )
            ]
        };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageValidation.Validate(package));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(ProtocolPayloadCodec.Encode<IProtocolMessage>(package))
        );
    }

    [Fact]
    public void TerrainRequestsRejectInvalidMetadataAndDuplicateCoordinates()
    {
        var id = new ChunkAllocationId(new Point2(1, 2), 1);
        foreach (var requests in new ChunkContentRequest[][]
                 {
                     [new ChunkContentRequest(id with { Generation = 0 })],
                     [new ChunkContentRequest(id, -1)],
                     [new ChunkContentRequest(id), new ChunkContentRequest(id with { Generation = 2 })]
                 })
        {
            var package = new TerrainChunksRequestPackage { ChunkRequests = requests };
            Assert.Throws<InvalidDataException>(() => ProtocolMessageValidation.Validate(package));
            Assert.Throws<InvalidDataException>(() =>
                ProtocolMessageCodec.Decode(ProtocolPayloadCodec.Encode<IProtocolMessage>(package))
            );
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void FragmentRepairsRejectInvalidAllocationAndContentVersions(int generation, long version)
    {
        var package = new TerrainFragmentsRequestPackage
        {
            FragmentRequests =
            [
                new TerrainChunkFragmentRequest(
                    new ChunkAllocationId(new Point2(1, 2), (ulong)generation),
                    version,
                    1,
                    [0]
                )
            ]
        };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageValidation.Validate(package));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(package));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(ProtocolPayloadCodec.Encode<IProtocolMessage>(package))
        );
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
