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
                new CharacterDataPackage
                {
                    Type = mode, CreationRequestId = _id, CreationError = CharacterCreationError.NameUnavailable,
                    CharacterName = "Test", CharacterId = _id, SkinName = "test.png", CharacterSex = 1,
                    UpdateLocation = new TerrainUpdateLocation
                    {
                        Center = new Vector2(-17, 5), ContentDistance = 64, VisibilityDistance = 32,
                        LastChunksUpdateCenter = Vector2.One
                    },
                }
            ];
        }

        foreach (var mode in Enum.GetValues<TeamManagePackage.CommandType>())
        {
            yield return
            [
                new TeamManagePackage
                {
                    Command = mode, OperationId = _id, Initiator = _address, Responder = _address, TeamId = _id,
                    Teams =
                    [
                        new TeamManagePackage.TeamState
                            { TeamId = _id, Name = "Test", Leader = _address, Members = [_address] }
                    ]
                }
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void SocialMessagesRoundTripWithoutGameLocalizationOrSubsystems(IProtocolMessage message)
    {
        Assert.True(ProtocolMessageCodec.IsDefined(message.ID));
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        var bytes = writer.Data(CompressionPolicy.None);
        using var reader = new ProtocolReader(bytes);
        var restored = ProtocolMessageCodec.Read(message.ID, reader);
        Assert.Equal(message.GetType(), restored.GetType());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        using var repeated = new ProtocolWriter();
        ProtocolMessageCodec.Write(restored, repeated);
        Assert.Equal(bytes, repeated.Data(CompressionPolicy.None));
    }

    [Fact]
    public void TruncatedGroupMembersAreRejectedBeforeAllocation()
    {
        using var writer = new ProtocolWriter();
        writer.WriteEnum(TeamManagePackage.CommandType.SyncTeams);
        writer.Write((ushort)1);
        writer.Write(_id);
        writer.Write("Test");
        PlayerAddressCodec.Write(writer, _address);
        writer.Write(ushort.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.TeamManage, reader));
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void RetiredCharacterDataModesAreNotParsed(byte tag)
    {
        using var writer = new ProtocolWriter();
        writer.Write(tag);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.CharacterData, reader));
    }
}
