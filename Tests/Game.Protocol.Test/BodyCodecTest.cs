using Engine.Core.Mathematics;

using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class BodyCodecTest
{
    [Fact]
    public void BodyStateBatchRoundTripsAllMovementComponents()
    {
        var items = Enumerable.Range(1, 100).Select(id => new SubsystemBodyPackage.BodyItem
            {
                CreatureId = id, Position = new Vector3(id, 80, id), Velocity = new Vector3(1, 0, 2),
                Rotation = Quaternion.Identity,
                ChangeFlag = SubsystemBodyPackage.ChangeFlag.PositionChange |
                             SubsystemBodyPackage.ChangeFlag.VelocityChange |
                             SubsystemBodyPackage.ChangeFlag.RotationChange
            }
        ).ToArray();
        var message = new BodyStatesPackage { StateTick = 42, BodyList = items };
        var restored =
            Assert.IsType<BodyStatesPackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        Assert.Equal(message.StateTick, restored.StateTick);
        Assert.Equal(items, restored.BodyList);
        Assert.True(typeof(SubsystemBodyPackage.BodyItem).IsValueType);
    }

    [Theory]
    [InlineData(SubsystemBodyPackage.EventType.ApplyImpulse)]
    [InlineData(SubsystemBodyPackage.EventType.HandleAxisCollision)]
    public void ActionsRoundTripWithoutRuntimeComponents(SubsystemBodyPackage.EventType type)
    {
        SubsystemBodyPackage message = type == SubsystemBodyPackage.EventType.ApplyImpulse
            ? new BodyImpulsePackage { CreatureId = 5, Impulse = Vector3.One }
            : new BodyCollisionPackage { CreatureId = 5, TargetCreatureId = 6, Impulse = Vector3.One };
        var encoded = ProtocolMessageCodec.Encode(message);
        var restored = ProtocolMessageCodec.Decode(encoded);
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(encoded, ProtocolMessageCodec.Encode(restored));
    }

    [Fact]
    public void TruncatedBodyCountIsRejectedBeforeAllocatingTheCollection()
    {
        var malformed = WireTestData.OversizedArray(new BodyStatesPackage { StateTick = 1 }, 1);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
