using Engine.Core.Mathematics;

using Game.Protocol.Blocks;
using Game.Protocol.Entities;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class WorldStateCodecTest
{
    public static TheoryData<string, IProtocolMessage> Samples => new()
    {
        {
            "vital-state",
            new VitalStatePackage
                { EntityId = 42, Food = 0.25f, Sleep = 0.5f, Stamina = 0.75f, Temperature = 20, Wetness = 0.125f }
        },
        {
            "skin-reply",
            new SkinReplyPackage { SkinName = "test.png", SkinData = [1, 2, 3, 4] }
        },
        { "furnace-state", new ComponentFurnacePackage(42, 3.5f, 0.25f, 2) },
        {
            "electricity-state",
            new SubsystemElectricityPackage(
                new ElectricitySnapshot
                {
                    StartStep = 42, IsBaseline = true,
                    SaveData = new Dictionary<Point3, float> { [new Point3(-3, 64, 2)] = 0.75f },
                    Removed = [new Point3(4, 65, -6)]
                }
            )
        },
        {
            "sign-state",
            new SignBlockPackage(
                new Point3(-3, 64, 2),
                ["one", "two"],
                [Color.Black, Color.White],
                "https://example.com"
            )
        },
        {
            "block-edit",
            new CrossbowPullPackage { InventoryId = 42, SlotIndex = 3 }
        }
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void WorldStateMessagesRoundTripTheirDeclaredContracts(string name, IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()), name);
        var encoded = ProtocolMessageCodec.Encode(message);
        var restored = ProtocolMessageCodec.Decode(encoded);
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(encoded, ProtocolMessageCodec.Encode(restored));
    }

    [Fact]
    public void SignLineCountIsIndependentFromColorCount()
    {
        var message = new SignBlockPackage(Point3.Zero, ["one", "two", "three"], [Color.White], string.Empty);
        var writer = ProtocolMessageCodec.Encode(message);
        var reader = writer;
        var restored = Assert.IsType<SignBlockPackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(message.Lines, restored.Lines);
        Assert.Equal(message.Colors, restored.Colors);
    }
}
