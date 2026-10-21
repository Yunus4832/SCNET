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
                new PickablePackage
                {
                    Type = type, StateTick = 42, Id = 7, Count = 3, Value = 4, Position = Vector3.One,
                    Velocity = Vector3.UnitZ,
                    StuckMatrix = Matrix.Identity, PlaySound = true, Positions = [new PickablePosition(7, Vector3.One)],
                    Pickables = [new PickableSnapshot(7, 300, 4, Vector3.One, Vector3.UnitZ, Matrix.Identity)],
                    RemovedIds = [7, 8]
                }
            ];
        }

        foreach (var type in Enum.GetValues<ProjectilePackage.EventType>())
        {
            yield return
            [
                new ProjectilePackage
                {
                    Type = type, Effect = ProjectilePackage.EffectType.Sizzle, NetworkId = 7, StateTick = 42, Value = 4,
                    Position = Vector3.One,
                    Rotation = Vector3.UnitX, Velocity = Vector3.UnitZ, AngularVelocity = Vector3.UnitY,
                    TrailOffset = Vector3.One, OwnerId = 8, IsFireProjectile = true
                }
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void ItemMessagesRoundTripPreparedValues(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(bytes);
        var restored = ProtocolMessageCodec.Read(message.ID, reader);
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter();
        ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(bytes, repeated.Data(CompressionPolicy.None));
    }

    [Theory]
    [InlineData(PickablePackage.PickType.Update)]
    [InlineData(PickablePackage.PickType.CreateList)]
    [InlineData(PickablePackage.PickType.DeleteList)]
    public void InvalidItemCountsAreRejectedBeforeAllocation(PickablePackage.PickType mode)
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(mode);
        writer.Write(42u);
        writer.Write(int.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.Pickable, reader));
    }

    [Fact]
    public void PositionBatchEntriesAreCompactValueTypes()
    {
        Assert.True(typeof(PickablePosition).IsValueType);
        Assert.True(System.Runtime.CompilerServices.Unsafe.SizeOf<PickablePosition>() <= 16);
    }
}
