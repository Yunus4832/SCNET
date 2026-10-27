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

    public static TheoryData<IProtocolMessage> Messages =>
    [
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
        new MountRequestPackage { FromId = 3, TargetId = 4 },
        new SleepResultPackage
            { Reason = "Reason", EntityId = 3, AllowManualWakeup = true, Result = false },

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
    ];

    [Theory]
    [MemberData(nameof(Messages))]
    public void StateMessagesRoundTripWithoutRuntimeObjects(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var encoded = writer;
        var reader = encoded;
        var restored = ProtocolMessageCodec.Decode(reader);
        Assert.Equal(message.GetType(), restored.GetType());

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(encoded, repeated);
    }

    [Fact]
    public void ModEnvelopePreservesItsTypedRoutingAndOpaquePayload()
    {
        var message = new ModEnvelopePackage("test.example", "update", [1, 2, 3, 4]);
        var restored =
            Assert.IsType<ModEnvelopePackage>(ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message)));
        Assert.Equal(message.ModId, restored.ModId);
        Assert.Equal(message.MessageType, restored.MessageType);
        Assert.Equal(message.Payload, restored.Payload);
    }

    [Theory]
    [InlineData(typeof(CharacterListPackage))]
    [InlineData(typeof(OnlineCharacterStatePackage))]
    public void TruncatedListsAreRejectedBeforeAllocatingEntries(Type type)
    {
        IProtocolMessage message = type == typeof(CharacterListPackage)
            ? new CharacterListPackage([])
            : new OnlineCharacterStatePackage([]);
        var malformed = WireTestData.OversizedArray(message, 0);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
