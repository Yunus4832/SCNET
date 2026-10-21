using Engine.Core.Mathematics;

using Game.Protocol.Blocks;
using Game.Protocol.Entities;
using Game.Protocol.Serialization;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class EntityEventCodecTest
{
    public static IEnumerable<object[]> Messages()
    {
        yield return
        [
            new ComponentOnFirePackage
            {
                Type = ComponentOnFirePackage.EventType.ComponentOnFire, EntityId = 42, AttackerEntityId = 43,
                Duration = 5
            }
        ];
        yield return [new ComponentOnFirePackage(-2, 64, 3, 0.5f)];
        yield return [new ComponentOnFirePackage(-2, 64, 3)];
        foreach (var type in Enum.GetValues<ComponentBehaviorPackage.EventType>())
        {
            yield return
            [
                new ComponentBehaviorPackage
                    { EntityId = 42, PackageEventType = type, RowLeft = true, RowRight = false, Type = 3 }
            ];
        }

        foreach (var type in Enum.GetValues<ComponentHealthPackage.EventType>())
        {
            yield return
            [
                new ComponentHealthPackage
                {
                    Type = type, TargetId = 42, AttackerId = 43, Health = 0.5f, Amount = 0.25f, Cause = "test",
                    IgnoreInvulnerability = true,
                    InjureRequestType = ComponentHealthPackage.RequestInjureType.Fall, Position = Vector3.One,
                    Velocity = Vector3.UnitY, Color = Color.White, Text = "7"
                }
            ];
        }

        foreach (var type in Enum.GetValues<MovingBlockPackage.EventType>())
        {
            yield return
            [
                new MovingBlockPackage
                {
                    Type = type, NetworkId = 42, StateTick = 7, AddData = [1, 2, 3, 4], Position = Vector3.One,
                    Velocity = Vector3.UnitX, Speed = 0.5f
                }
            ];
        }

        yield return [new ExplosionsPackage(Vector3.One, 2, 0.5f)];
        yield return
        [
            new ExplosionsPackage
            {
                Type = ExplosionsPackage.EventType.Cell, Cells = new Dictionary<Point2, IReadOnlyList<ExplosionCell>>
                    { [new Point2(-1, 2)] = new ExplosionCell[] { new(new Point3(-2, 64, 3), 0.5f) } }
            }
        ];
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void EntityEventsRoundTripEveryModeWithoutRuntimeDependencies(IProtocolMessage message)
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

    [Fact]
    public void ExplosionChunkCountMustFitTheFrame()
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(ExplosionsPackage.EventType.Cell);
        writer.Write(int.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.Explosion, reader));
    }
}
