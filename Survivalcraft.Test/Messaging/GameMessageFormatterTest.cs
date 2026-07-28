using Game.Messaging;

namespace Survivalcraft.Test.Messaging;

public class GameMessageFormatterTest
{
    [Fact]
    public void CommandMessageHasSingleSemanticPrefix()
    {
        var formatted = GameMessageFormatter.Format(
            GameMessage.Command("世界时间已更新。", success: true));

        Assert.Equal("[指令]世界时间已更新。", formatted.PlainText);
        Assert.Equal(MessageTextStyle.Success, formatted.Segments[0].Style);
        Assert.DoesNotContain("[系统]", formatted.PlainText);
    }

    [Fact]
    public void TeamChatKeepsChannelSenderAndBodySeparate()
    {
        var formatted = GameMessageFormatter.Format(
            GameMessage.Chat(GameMessageChannel.Team, "Lily", "hello"));

        Assert.Equal("[队][Lily]hello", formatted.PlainText);
        Assert.Collection(
            formatted.Segments,
            segment => Assert.Equal(MessageTextStyle.Team, segment.Style),
            segment => Assert.Equal(MessageTextStyle.Sender, segment.Style),
            segment => Assert.Equal(MessageTextStyle.Normal, segment.Style));
    }

    [Fact]
    public void ChatProtocolOnlyExposesGlobalAndTeamChannels()
    {
        Assert.Equal(
            [nameof(GameMessageChannel.Global), nameof(GameMessageChannel.Team)],
            Enum.GetNames<GameMessageChannel>());
    }
}
