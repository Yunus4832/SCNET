using Engine.Core;

using Game.Protocol.Blocks;
using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class WorldStateCodecTest
{
    public static TheoryData<string, IProtocolMessage> Samples => new()
    {
        { "vital-state", new ComponentVitalStatPackage { EntityId = 42, Food = 0.25f, Sleep = 0.5f, Stamina = 0.75f, Temperature = 20, Wetness = 0.125f } },
        { "skin-reply", new ComponentClothingPackage("test.png", ComponentClothingPackage.DataType.ReplySkin) { SkinData = [1, 2, 3, 4] } },
        { "furnace-state", new ComponentFurnacePackage(42, 3.5f, 0.25f, 2) },
        { "electricity-state", new SubsystemElectricityPackage(new ElectricitySnapshot { StartStep = 42, IsBaseline = true, SaveData = new Dictionary<Point3, float> { [new Point3(-3, 64, 2)] = 0.75f }, Removed = [new Point3(4, 65, -6)] }) },
        { "sign-state", new SignBlockPackage(new Point3(-3, 64, 2), ["one", "two"], [Color.Black, Color.White], "https://example.com") },
        { "block-edit", new BlockEditPackage { Type = BlockEditPackage.EventType.CrossbowPull, InventoryId = 42, SlotIndex = 3 } }
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void EncodingMatchesCapturedPayloadWithCurrentMessageId(string name, IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88); writer.Write(message.ID); ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".bin")), bytes);
        using var reader = new ProtocolReader(bytes);
        Assert.Equal(0x88, reader.ReadByte());
        var restored = ProtocolMessageCodec.Read(reader.ReadByte(), reader);
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter();
        ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(ProtocolFrameCodec.DecodeFrame(bytes)[2..], ProtocolFrameCodec.DecodeFrame(repeated.Data(CompressionPolicy.None)));
    }

    [Fact]
    public void SignLineCountIsIndependentFromColorCount()
    {
        var message = new SignBlockPackage(Point3.Zero, ["one", "two", "three"], [Color.White], string.Empty);
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        var restored = Assert.IsType<SignBlockPackage>(ProtocolMessageCodec.Read(message.ID, reader));
        Assert.Equal(message.Lines, restored.Lines);
        Assert.Equal(message.Colors, restored.Colors);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
