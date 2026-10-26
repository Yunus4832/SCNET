using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game.Protocol.Blocks;
using Game.Protocol.Characters;
using Game.Protocol.Entities;
using Game.Protocol.Identity;
using Game.Protocol.Mods;
using Game.Protocol.Serialization;
using Game.Protocol.World;

namespace Game.Protocol.Test;

public sealed class EntityStateCodecTest
{
    private static readonly Guid _id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static TheoryData<IProtocolMessage> Messages => new()
    {
        new DispenserPackage(new Point3(-2, 64, 3), 4),
        new ComponentSicknessPackage { EntityId = 3, SicknessDuration = 4.5f },
        new ModEnvelopePackage("test.example", "update", [1, 2, 3, 4]),
        new CharacterListPackage(
            [
                new CharacterListEntry(
                    _id,
                    new PlayerAddress(
                        IdentityAddress.Encode(
                            Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000")
                        )
                    ),
                    "Test",
                    true
                )
            ]
        ),
        new OnlineCharacterStatePackage([new OnlineCharacterState(_id, Vector3.One, 0.5f, true)]),
        new ComponentMountPackage(3, 4, true),
        new ComponentSleepPackage(3, ComponentSleepPackage.EventType.Sleep, true, false, "Reason"),
        new SubsystemWeatherPackage
        {
            PrecipitationStartTime = 2, PrecipitationEndTime = 3, LightningIntensity = 0.5f, FogStartTime = 4,
            FogRampTime = 5, FogEndTime = 6
        },
        new ComponentFluPackage
        {
            EntityId = 3, PackageEventType = ComponentFluPackage.EventType.Sneeze, FluOnset = 4, FluDuration = 5,
            CoughDuration = 6, SneezeDuration = 7
        }
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void StateMessagesRoundTripWithoutRuntimeObjects(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        var encoded = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(encoded);
        var restored = ProtocolMessageCodec.Read(message.ID, reader);
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter();
        ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(encoded, repeated.Data(CompressionPolicy.None));
    }

    [Fact]
    public void ModEnvelopeMatchesPreRefactorCapture()
    {
        var message = new ModEnvelopePackage("test.example", "update", [1, 2, 3, 4]);
        using var writer = new ProtocolWriter();
        writer.Write((byte)0x88);
        writer.Write(message.ID);
        ProtocolMessageCodec.Write(message, writer);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mod-envelope.bin")),
            writer.Data(CompressionPolicy.None)
        );
    }

    [Theory]
    [InlineData(PackageType.CharacterList)]
    [InlineData(PackageType.OnlineCharacterState)]
    public void TruncatedListsAreRejectedBeforeAllocatingEntries(PackageType type)
    {
        using var writer = new ProtocolWriter();
        writer.Write(ushort.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)type, reader));
    }
}
