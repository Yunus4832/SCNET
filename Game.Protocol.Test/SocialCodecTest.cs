using Engine.Core.Mathematics;

using Game.Protocol.Characters;
using Game.Protocol.Identity;
using Game.Protocol.Messaging;
using Game.Protocol.Serialization;
using Game.Protocol.Teams;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class SocialCodecTest
{
    private static readonly Guid _id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static readonly PlayerAddress _address = new(Engine.Core.Security.IdentityAddress.Encode(new byte[32]));

    public static IEnumerable<object[]> Messages()
    {
        yield return
        [
            new MessagePackage(GameMessage.Chat(GameMessageChannel.Team, new MessageSender(_address, "Test"), "hello"))
        ];
        yield return
        [
            new MessagePackage(
                GameMessage.System([new MessageSegment("warning", MessageTextStyle.Warning, Color.White)]) with
                {
                    LocalizationSection = "Test", LocalizationKey = "Message",
                    LocalizationArguments = ["one", "two"]
                }
            )
        ];
        foreach (var mode in Enum.GetValues<CharacterDataPackage.DataType>())
        {
            yield return
            [
                (mode switch
                {
                    CharacterDataPackage.DataType.Create => (CharacterDataPackage)
                        new CharacterCreateRequestPackage
                        {
                            CreationRequestId = _id, CharacterName = "Test", SkinName = "test.png", CharacterSex = 1
                        },
                    CharacterDataPackage.DataType.Modify => new CharacterProfilePackage
                        { CharacterName = "Test", CharacterId = _id, SkinName = "test.png", CharacterSex = 1 },
                    CharacterDataPackage.DataType.CreationRejected => new
                        CharacterCreateRejectedPackage
                        { CreationRequestId = _id, CreationError = CharacterCreationError.NameUnavailable },
                    CharacterDataPackage.DataType.SetUpdateLocation => new
                        CharacterUpdateLocationPackage
                        {
                            UpdateLocation = new TerrainUpdateLocation
                            {
                                Center = new Vector2(-17, 5), ContentDistance = 64, VisibilityDistance = 32,
                                LastChunksUpdateCenter = Vector2.One
                            }
                        },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                })
            ];
        }

        foreach (var mode in Enum.GetValues<TeamManagePackage.CommandType>())
        {
            yield return
            [
                (mode switch
                {
                    TeamManagePackage.CommandType.PromptJoinRequest => (TeamManagePackage)
                        new TeamJoinPromptPackage
                            { OperationId = _id, Initiator = _address, Responder = _address, TeamId = _id },
                    TeamManagePackage.CommandType.PromptInvitation => new
                        TeamInvitationPromptPackage
                        { OperationId = _id, Initiator = _address, Responder = _address, TeamId = _id },
                    TeamManagePackage.CommandType.SyncTeams => new TeamSnapshotPackage
                    {
                        Teams =
                        [
                            new TeamManagePackage.TeamState
                                { TeamId = _id, Name = "Test", Leader = _address, Members = [_address] }
                        ]
                    },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                })
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void SocialMessagesRoundTripWithoutGameLocalizationOrSubsystems(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var bytes = writer;
        var reader = bytes;
        var restored = ProtocolMessageCodec.Decode(reader);
        Assert.Equal(message.GetType(), restored.GetType());

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(bytes, repeated);
    }

    [Fact]
    public void TruncatedGroupMembersAreRejectedBeforeAllocation()
    {
        var malformed = WireTestData.OversizedArray(new TeamSnapshotPackage(), 0);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void UnknownCreationErrorsAreRejected(byte tag)
    {
        var message = new CharacterCreateRejectedPackage();
        var malformed = WireTestData.ReplaceField(message, 1, MessagePack.MessagePackSerializer.Serialize(tag));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
