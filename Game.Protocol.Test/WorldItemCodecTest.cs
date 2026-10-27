using Engine.Core.Mathematics;

using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class WorldItemCodecTest
{
    public static IEnumerable<object[]> Messages()
    {
        foreach (var type in Enum.GetValues<PickablePackage.PickType>())
        {
            yield return
            [
                (type switch
                {
                    PickablePackage.PickType.Create => (PickablePackage)
                        new PickableAddedPackage
                        {
                            StateTick = 42, Id = 7, Count = 3, Value = 4, Position = Vector3.One,
                            Velocity = Vector3.UnitZ, StuckMatrix = Matrix.Identity
                        },
                    PickablePackage.PickType.Update => new PickablePositionsPackage
                        { StateTick = 42, Positions = [new PickablePosition(7, Vector3.One)] },
                    PickablePackage.PickType.Delete => new PickableRemovedPackage
                        { StateTick = 42, Id = 7, PlaySound = true },
                    PickablePackage.PickType.RequestSync => new PickableSyncRequestPackage
                        { StateTick = 42, Id = 7 },
                    PickablePackage.PickType.CreateList => new PickablesAddedPackage
                    {
                        StateTick = 42,
                        Pickables = [new PickableSnapshot(7, 300, 4, Vector3.One, Vector3.UnitZ, Matrix.Identity)]
                    },
                    PickablePackage.PickType.DeleteList => new PickablesRemovedPackage
                        { StateTick = 42, RemovedIds = [7, 8] },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                })
            ];
        }

        foreach (var type in Enum.GetValues<ProjectilePackage.EventType>())
        {
            yield return
            [
                (type switch
                {
                    ProjectilePackage.EventType.Add => (ProjectilePackage)new
                        ProjectileAddedPackage
                        {
                            NetworkId = 7, StateTick = 42, Value = 4, Position = Vector3.One,
                            Rotation = Vector3.UnitX, Velocity = Vector3.UnitZ, AngularVelocity = Vector3.UnitY,
                            TrailOffset = Vector3.One, OwnerId = 8, IsFireProjectile = true
                        },
                    ProjectilePackage.EventType.Remove => new ProjectileRemovedPackage
                        { NetworkId = 7 },
                    ProjectilePackage.EventType.Update => new ProjectileStatePackage
                    {
                        NetworkId = 7, StateTick = 42, Position = Vector3.One, Rotation = Vector3.UnitX,
                        Velocity = Vector3.UnitZ, AngularVelocity = Vector3.UnitY
                    },
                    ProjectilePackage.EventType.Effect => new ProjectileEffectPackage
                    {
                        Effect = ProjectilePackage.EffectType.Sizzle, NetworkId = 7, Value = 4,
                        Position = Vector3.One
                    },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                })
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void ItemMessagesRoundTripPreparedValues(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var bytes = writer;
        var reader = bytes;
        var restored = ProtocolMessageCodec.Decode(reader);
        Assert.Equal(message.GetType(), restored.GetType());

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(bytes, repeated);
    }

    [Theory]
    [InlineData(PickablePackage.PickType.Update)]
    [InlineData(PickablePackage.PickType.CreateList)]
    [InlineData(PickablePackage.PickType.DeleteList)]
    public void InvalidItemCountsAreRejectedBeforeAllocation(PickablePackage.PickType mode)
    {
        IProtocolMessage message = mode switch
        {
            PickablePackage.PickType.Update => new PickablePositionsPackage(),
            PickablePackage.PickType.CreateList => new PickablesAddedPackage(),
            _ => new PickablesRemovedPackage()
        };
        var malformed = WireTestData.OversizedArray(message, 1);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }

    [Fact]
    public void PositionBatchEntriesAreCompactValueTypes()
    {
        Assert.True(typeof(PickablePosition).IsValueType);
        Assert.True(System.Runtime.CompilerServices.Unsafe.SizeOf<PickablePosition>() <= 16);
    }
}
