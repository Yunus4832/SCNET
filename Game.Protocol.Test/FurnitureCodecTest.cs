using Engine.Core;

using Game.Protocol.Blocks;
using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class FurnitureCodecTest
{
    public static IEnumerable<object[]> Modes() =>
        Enum.GetValues<FurniturePackage.EventType>().Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(Modes))]
    public void FurnitureModesCarryOnlyPreparedPayload(FurniturePackage.EventType type)
    {
        var message = new FurniturePackage
        {
            PackageEventType = type,
            AddXml = "opaque game design or set name",
            FromName = "source",
            FurnitureIndex = 42,
            CellFace = new CellFace(-3, 64, 2, 1),
            StartValue = 7,
            PointDict = new Dictionary<Point3, int> { [new Point3(-3, 64, 2)] = 7 },
            ToRemoveList = [4, 5]
        };
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(bytes);
        var restored = Assert.IsType<FurniturePackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(type, restored.PackageEventType);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter();
        ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(bytes, repeated.Data(CompressionPolicy.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void RemovalCountIsBoundedBeforeAllocation(int count)
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(FurniturePackage.EventType.RemoveFurnitureDesigns);
        writer.Write(count);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.Furniture, reader));
    }
}
