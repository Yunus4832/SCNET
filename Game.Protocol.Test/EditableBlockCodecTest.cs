using Game.Protocol.Blocks;
using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class EditableBlockCodecTest
{
    public static IEnumerable<object[]> Modes()
    {
        foreach (var item in Enum.GetValues<BuiltInBlockEditKind>())
        {
            yield return [item, false, false];
            yield return [item, true, false];
            yield return [item, false, true];
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EditablePayloadAndTargetRoundTripWithoutRuntimeEditors(BuiltInBlockEditKind item, bool inventory, bool definition)
    {
        var message = new EditableBlockPackage
        {
            ItemType = item,
            EditAsItem = inventory,
            SyncItem = definition,
            InventoryId = 42,
            SlotIndex = 3,
            CellFace = new CellFace(-3, 64, 2, 1),
            Id = 7,
            Data = [1, 2, 3, 4],
            Delay = 10
        };
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter(); ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(bytes);
        var restored = Assert.IsType<EditableBlockPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(item, restored.ItemType);
        Assert.Equal(inventory && !definition, restored.EditAsItem);
        Assert.Equal(definition, restored.SyncItem);
        if (!inventory && !definition) { Assert.Equal(message.CellFace, restored.CellFace); }
        if (item is BuiltInBlockEditKind.MemoryBank or BuiltInBlockEditKind.TruthTable)
        { Assert.Equal(message.Data, restored.Data); Assert.Equal(message.Id, restored.Id); }
        else { Assert.Equal(message.Delay, restored.Delay); }
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter(); ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(bytes, repeated.Data(CompressionPolicy.None));
    }

    [Fact]
    public void OversizedDefinitionCannotSilentlyWrapItsLength()
    {
        var message = new EditableBlockPackage { ItemType = BuiltInBlockEditKind.MemoryBank, Data = new byte[65536] };
        using var writer = new ProtocolWriter();
        Assert.Throws<ArgumentException>(() => ProtocolMessageCodec.Write(message, writer));
    }

    [Fact]
    public void CellFaceGeometryAndCodecAreOwnedBelowTheGame()
    {
        var face = new CellFace(-3, 64, 2, 1);
        using var writer = new ProtocolWriter(); writer.Write(face);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Equal(face, reader.ReadCellFace());
        Assert.Equal(typeof(GameProtocol).Assembly, typeof(CellFace).Assembly);
    }
}
