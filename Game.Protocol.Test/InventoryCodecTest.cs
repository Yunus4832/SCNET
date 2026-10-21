using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class InventoryCodecTest
{
    public static IEnumerable<object[]> Modes() =>
        Enum.GetValues<ComponentInventoryPackage.EventType>().Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(Modes))]
    public void InventoryModesRoundTripWithoutLiveInventories(ComponentInventoryPackage.EventType type)
    {
        var message = new ComponentInventoryPackage
        {
            PackageEventType = type,
            InventoryID = 42,
            ActiveSlot = 3,
            DragMode = 1,
            ProcessingOnly = true,
            SourceInventorySlot = new InventorySlot { InventoryId = 42, SlotIndex = 3, Value = 7, Count = 4 },
            TargetInventorySlot = new InventorySlot { InventoryId = 43, SlotIndex = 5, Count = 2 },
            Slots = new Dictionary<int, IReadOnlyList<InventorySlotSnapshot>>
            {
                [42] = new InventorySlotSnapshot[]
                {
                    new() { SlotIndex = 3, Type = 0, Count = 4, Value = 7 },
                    new() { SlotIndex = 5, Type = 1, ClothingList = new[] { 12, 15 } }
                }
            }
        };
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(bytes);
        var restored = Assert.IsType<ComponentInventoryPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(type, restored.PackageEventType);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter();
        ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(bytes, repeated.Data(CompressionPolicy.None));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(255)]
    public void UnsupportedInventoryModesAreRejected(byte mode)
    {
        using var writer = new ProtocolWriter();
        writer.Write(mode);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Read((byte)PackageType.ComponentInventory, reader)
        );
    }

    [Fact]
    public void TruncatedInventoryCountIsRejectedBeforeAllocation()
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(ComponentInventoryPackage.EventType.InventorySync);
        writer.Write(int.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Read((byte)PackageType.ComponentInventory, reader)
        );
    }
}
