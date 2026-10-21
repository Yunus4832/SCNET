using Engine.Core.Mathematics;

using Game.Protocol.Connection;
using Game.Protocol.Serialization;
using Game.Protocol.Terrain;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class SimpleMessageTest
{
    public static IEnumerable<object[]> Messages =>
    [
        [
            new ConnectionPhaseAckPackage(
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                ConnectionPhase.BootstrapApplied
            )
        ],
        [new ChunkStateResetPackage(new Point2(-2, 3))],
        [new SubsystemTimePackage(1234.5, 0.375)],
        [new SubsystemSkyPackage(new Vector3(-100, 80, 200))],
        [new SubsystemSeasonPackage(0.625f)]
    ];

    [Theory]
    [MemberData(nameof(Messages))]
    public void SimpleRecordsRoundTrip(IProtocolMessage message)
    {
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var result = ProtocolMessageCodec.Read(message.ID, reader);
        Assert.Equal(message, result);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void UnknownConnectionPhaseIsRejected()
    {
        using var writer = new ProtocolWriter();
        writer.Write(Guid.NewGuid());
        writer.Write((byte)255);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Read((byte)PackageType.ConnectionPhaseAck, reader)
        );
    }
}
