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
        var payload = ProtocolMessageCodec.Encode(message);
        Assert.Equal(message, ProtocolMessageCodec.Decode(payload));
    }

    [Fact]
    public void UnknownConnectionPhaseIsRejected()
    {
        var message = new ConnectionPhaseAckPackage(Guid.NewGuid(), ConnectionPhase.BootstrapApplied);
        var malformed = WireTestData.ReplaceField(message, 1, [0xcc, 255]);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
