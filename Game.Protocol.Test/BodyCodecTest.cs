using Engine.Core.Mathematics;

using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class BodyCodecTest
{
    [Fact]
    public void BodyStateEncodingMatchesIndependentPreRefactorCapture()
    {
        var items = new SubsystemBodyPackage.BodyItem[100];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new SubsystemBodyPackage.BodyItem
            {
                CreatureId = i + 1,
                Position = new Vector3(i, 80, i),
                Velocity = new Vector3(1, 0, 2),
                Rotation = Quaternion.Identity,
                ChangeFlag = SubsystemBodyPackage.ChangeFlag.PositionChange |
                             SubsystemBodyPackage.ChangeFlag.VelocityChange |
                             SubsystemBodyPackage.ChangeFlag.RotationChange
            };
        }

        var message = new SubsystemBodyPackage { StateTick = 42, BodyList = items };
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88);
        writer.Write(message.ID);
        ProtocolMessageCodec.Write(message, writer);
        var encoded = writer.Data(CompressionPolicy.None);
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "body-state.bin")), encoded);
        using var reader = new ProtocolReader(encoded);
        Assert.Equal(0x88, reader.ReadByte());
        var restored = Assert.IsType<SubsystemBodyPackage>(ProtocolMessageCodec.Read(reader.ReadByte(), reader));
        Assert.Equal(message.StateTick, restored.StateTick);
        Assert.Equal(items, restored.BodyList);
        Assert.True(typeof(SubsystemBodyPackage.BodyItem).IsValueType);
    }

    [Theory]
    [InlineData(SubsystemBodyPackage.EventType.ApplyImpulse)]
    [InlineData(SubsystemBodyPackage.EventType.HandleAxisCollision)]
    public void ActionsRoundTripWithoutRuntimeComponents(SubsystemBodyPackage.EventType type)
    {
        var message = new SubsystemBodyPackage
            { PackageEventType = type, CreatureId = 5, TargetCreatureId = 6, Impulse = Vector3.One };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<SubsystemBodyPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(type, restored.PackageEventType);
        Assert.Equal(message.CreatureId, restored.CreatureId);
        Assert.Equal(message.Impulse, restored.Impulse);
        if (type == SubsystemBodyPackage.EventType.HandleAxisCollision)
        {
            Assert.Equal(message.TargetCreatureId, restored.TargetCreatureId);
        }

        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Fact]
    public void TruncatedBodyCountIsRejectedBeforeAllocatingTheCollection()
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(SubsystemBodyPackage.EventType.BodyUpdate);
        writer.Write(1u);
        writer.Write(ushort.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.SubsystemBody, reader));
    }
}
