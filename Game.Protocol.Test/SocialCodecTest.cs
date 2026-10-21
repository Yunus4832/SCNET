using Engine.Core.Mathematics;

using Game.Protocol.Messaging;
using Game.Protocol.Players;
using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class SocialCodecTest
{
    private static readonly Guid _id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static IEnumerable<object[]> Messages()
    {
        yield return [new MessagePackage(GameMessage.Chat(GameMessageChannel.Team, "Test", "hello"))];
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
        foreach (var mode in Enum.GetValues<PlayerDataPackage.DataType>())
        {
            yield return
            [
                new PlayerDataPackage
                {
                    Type = mode, PlayerName = "Test", PlayerGuid = _id, SkinName = "test.png", PlayerClass = 1,
                    UpdateLocation = new TerrainUpdateLocation
                    {
                        Center = new Vector2(-17, 5), ContentDistance = 64, VisibilityDistance = 32,
                        LastChunksUpdateCenter = Vector2.One
                    },
                }
            ];
        }

        foreach (var mode in Enum.GetValues<GroupManagePackage.CommandType>())
        {
            yield return
            [
                new GroupManagePackage
                {
                    Command = mode, OperationId = _id, FromPlayer = _id, ToPlayer = _id, GroupKey = _id,
                    Groups = [new GroupManagePackage.GroupState { GroupKey = _id, Name = "Test", Members = [_id] }]
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
        writer.WriteEnum(GroupManagePackage.CommandType.SyncGroups);
        writer.Write((ushort)1);
        writer.Write(_id);
        writer.Write("Test");
        writer.Write(ushort.MaxValue);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.GroupManage, reader));
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void RetiredPlayerDataModesAreNotParsed(byte tag)
    {
        using var writer = new ProtocolWriter();
        writer.Write(tag);
        using var reader = new ProtocolReader(writer.Data(CompressionPolicy.None));
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Read((byte)PackageType.PlayerData, reader));
    }
}
