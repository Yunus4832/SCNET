using System.Text.Json.Nodes;

using Game.Protocol.Blocks;
using Game.Protocol.Commands;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Localization;
using Game.Protocol.Mods;
using Game.Protocol.Serialization;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class MessageOwnershipTest
{
    [Fact]
    public void JsonValuesWrappingMutableObjectsDoNotRetainTheProducerObject()
    {
        var model = new MutableJsonModel();
        model.Items.Add(1);
        var original = new CommandResultPackage
        {
            Result = new CommandResponse(true, "ok", LocalizedText.Literal("done"), Data: JsonValue.Create(model))
        };
        var captured = Assert.IsType<CommandResultPackage>(ProtocolMessageOwnership.Capture(original));
        var before = captured.Result!.Data!.ToJsonString();
        model.Items.Add(2);
        Assert.Equal(before, captured.Result.Data.ToJsonString());
    }

    private sealed class MutableJsonModel
    {
        public List<int> Items { get; } = [];
    }

    [Fact]
    public void OpaqueModPayloadIsIndependentOfBothTheProducerAndAnotherCapture()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var original = new ModEnvelopePackage("example.mod", "update", bytes);
        var first = Assert.IsType<ModEnvelopePackage>(ProtocolMessageOwnership.Capture(original));
        var second = Assert.IsType<ModEnvelopePackage>(ProtocolMessageOwnership.Capture(first));
        bytes[0] = 9;
        first.Payload[1] = 8;
        Assert.Equal(new byte[] { 1, 8, 3 }, first.Payload);
        Assert.Equal(new byte[] { 1, 2, 3 }, second.Payload);
        Assert.Equal(new byte[] { 9, 2, 3 }, original.Payload);
    }

    [Fact]
    public void NestedInventoryMapsListsAndPolymorphicClothingAreCapturedRecursively()
    {
        var clothes = new List<int> { 7, 8 };
        var slots = new List<InventorySlotSnapshot> { new ClothingSlotSnapshot { SlotIndex = 1, Clothes = clothes } };
        var map = new Dictionary<int, IReadOnlyList<InventorySlotSnapshot>> { [3] = slots };
        var original = new InventorySnapshotPackage { Slots = map };
        var captured = Assert.IsType<InventorySnapshotPackage>(ProtocolMessageOwnership.Capture(original));
        var before = ProtocolMessageCodec.Encode(original);
        clothes.Clear();
        slots.Clear();
        map.Clear();
        Assert.Equal(new[] { 7, 8 }, Assert.IsType<ClothingSlotSnapshot>(Assert.Single(captured.Slots[3])).Clothes);
        Assert.Equal(before, ProtocolMessageCodec.Encode(captured));
    }

    [Fact]
    public void BootstrapCapturesItsNestedRosterAndBinaryContentsWithoutChangingTheWireValue()
    {
        var mapping = new List<BlockIndexEntry>();
        var clients = new List<ClientIdentity>();
        var data = new byte[] { 1, 2, 3 };
        var original = new BootstrapPackage
        {
            World = new WorldMetadata("World", 7, 0, 8), Epoch = Guid.NewGuid(), BlockMapping = mapping,
            ClientList = new ClientListPackage { List = clients }, ProjectData = data, TextureData = data
        };
        var captured = Assert.IsType<BootstrapPackage>(ProtocolMessageOwnership.Capture(original));
        var before = ProtocolMessageCodec.Encode(original);
        mapping.Add(new BlockIndexEntry(ResourceId.Parse("game:stone"), 1));
        data[0] = 99;
        Assert.Empty(captured.BlockMapping);
        Assert.NotSame(original.ClientList, captured.ClientList);
        Assert.NotSame(captured.ProjectData, captured.TextureData);
        Assert.Equal(before, ProtocolMessageCodec.Encode(captured));
    }

    [Fact]
    public void NullableResponsesAndJsonTreesHaveIndependentOwnership()
    {
        var arguments = new[] { LocalizedText.Literal("one") };
        var json = new JsonObject { ["values"] = new JsonArray(1, 2) };
        var original = new CommandResultPackage
        {
            Result = new CommandResponse(
                true,
                "ok",
                LocalizedText.Format(LocalizedText.Literal("done {0}"), arguments),
                Data: json
            )
        };
        var captured = Assert.IsType<CommandResultPackage>(ProtocolMessageOwnership.Capture(original));
        arguments[0] = LocalizedText.Literal("changed");
        json["values"]!.AsArray().Clear();
        Assert.Equal("one", captured.Result!.Message.Arguments[0].Fallback);
        Assert.Equal(2, captured.Result.Data!["values"]!.AsArray().Count);
        var noData = original with { Result = new CommandResponse(true, "ok", LocalizedText.Literal("done")) };
        var empty = Assert.IsType<CommandResultPackage>(ProtocolMessageOwnership.Capture(noData));
        Assert.Null(empty.Result!.Data);
        Assert.Equal(LocalizedText.Literal("done"), empty.Result.Message);
    }
}
