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
        var source = new InventorySlotReference { InventoryId = 42, SlotIndex = 3 };
        var target = new InventorySlotReference { InventoryId = 43, SlotIndex = 5 };
        ComponentInventoryPackage message = type switch
        {
            ComponentInventoryPackage.EventType.ActiveSlotChange => new InventoryActiveSlotPackage
                { InventoryID = 42, ActiveSlot = 3 },
            ComponentInventoryPackage.EventType.InventorySync => new InventorySnapshotPackage
            {
                Slots = new Dictionary<int, IReadOnlyList<InventorySlotSnapshot>>
                {
                    [42] =
                    [
                        new ItemSlotSnapshot { SlotIndex = 3, Count = 4, Value = 7 },
                        new ClothingSlotSnapshot { SlotIndex = 5, Clothes = [12, 15] }
                    ]
                }
            },
            ComponentInventoryPackage.EventType.QueryErrorInventoryInfo => new InventorySyncRequestPackage
                { InventoryID = 42 },
            ComponentInventoryPackage.EventType.SetSlotsItem => new InventorySetSlotPackage
                { InventoryId = 42, SlotIndex = 3, Value = 7, Count = 4 },
            ComponentInventoryPackage.EventType.HandleMoveItem => new InventoryMoveRequestPackage
                { Source = source, Target = target, Count = 2 },
            ComponentInventoryPackage.EventType.HandleDragDrop => new InventoryDragDropRequestPackage
                { Source = source, Target = target, DragMode = 1, ProcessingOnly = true },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var bytes = writer;
        var reader = bytes;
        var restored = Assert.IsAssignableFrom<ComponentInventoryPackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(type, restored.PackageEventType);

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(bytes, repeated);
    }

    [Fact]
    public void RequiredInventoryFieldsCannotBeNull()
    {
        var malformed = WireTestData.ReplaceField(new InventoryActiveSlotPackage(), 1, [0xc0]);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }

    [Fact]
    public void TruncatedInventoryCountIsRejectedBeforeAllocation()
    {
        var malformed = WireTestData.ReplaceField(new InventorySnapshotPackage(), 0, [0xdf, 0x7f, 0xff, 0xff, 0xff]);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
