using Game.Protocol.Blocks;
using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class EditableBlockCodecTest
{
    public static IEnumerable<object[]> Messages()
    {
        EditableBlockTarget[] targets =
            [new EditableWorldBlockTarget(new CellFace(-3, 64, 2, 1)), new EditableInventorySlotTarget(42, 3)];
        foreach (var target in targets.Append(new EditableItemDefinitionTarget(7)))
        {
            foreach (var kind in new[] { BuiltInBlockEditKind.MemoryBank, BuiltInBlockEditKind.TruthTable })
            {
                yield return
                [
                    new EditableBlockDataPackage
                        { ItemType = kind, Target = target, DefinitionId = 7, Data = [1, 2, 3, 4] }
                ];
            }
        }

        foreach (var target in targets)
        {
            foreach (var kind in Enum.GetValues<BuiltInBlockEditKind>()
                         .Except([BuiltInBlockEditKind.MemoryBank, BuiltInBlockEditKind.TruthTable]))
            {
                yield return [new EditableBlockValuePackage { ItemType = kind, Target = target, Value = 10 }];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void TargetAndPayloadVariantsRoundTrip(EditableBlockPackage message)
    {
        var encoded = ProtocolMessageCodec.Encode(message);
        var restored = Assert.IsAssignableFrom<EditableBlockPackage>(ProtocolMessageCodec.Decode(encoded));
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(message.ItemType, restored.ItemType);
        Assert.Equal(message.Target, restored.Target);
        Assert.Equal(encoded, ProtocolMessageCodec.Encode(restored));
    }

    [Fact]
    public void OversizedDefinitionIsRejectedOnBothSides()
    {
        var message = new EditableBlockDataPackage
            { ItemType = BuiltInBlockEditKind.MemoryBank, Target = new EditableItemDefinitionTarget(7), Data = [] };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(message with { Data = new byte[65536] }));
        var malformed = WireTestData.ReplaceField(
            message,
            3,
            MessagePack.MessagePackSerializer.Serialize(new byte[65536])
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }

    [Fact]
    public void CellFaceGeometryAndCodecAreOwnedBelowTheGame()
    {
        var face = new CellFace(-3, 64, 2, 1);
        Assert.Equal(face, ProtocolPayloadCodec.Decode<CellFace>(ProtocolPayloadCodec.Encode(face)));
        Assert.Equal(typeof(GameProtocol).Assembly, typeof(CellFace).Assembly);
    }
}
