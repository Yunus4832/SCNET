using System.Text.Json.Nodes;

using Engine.Core.Security;

using Game.Protocol.Identity;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Messaging;

[Collection(ConfigFileCollection.Name)]
public class GameMessageFormatterTest : IDisposable
{
    private readonly JsonObject _originalKeywords = Game.Localization.ClientLanguage.Catalog.KeyWords;

    public GameMessageFormatterTest()
    {
        // Exercise fallback labels independently of languages loaded by other test collections.
        Game.Localization.ClientLanguage.Catalog.KeyWords = new JsonObject();
    }

    public void Dispose()
    {
        Game.Localization.ClientLanguage.Catalog.KeyWords = _originalKeywords;
    }

    [Fact]
    public void CommandMessageHasSingleSemanticPrefix()
    {
        var formatted =
            Game.Localization.ClientLanguage.Formatter.Format(GameMessage.Command("世界时间已更新。", success: true));

        Assert.Equal("[指令]世界时间已更新。", formatted.PlainText);
        Assert.Equal(MessageTextStyle.Success, formatted.Segments[0].Style);
        Assert.DoesNotContain("[系统]", formatted.PlainText);
    }

    [Fact]
    public void TeamChatKeepsChannelSenderAndBodySeparate()
    {
        var formatted = Game.Localization.ClientLanguage.Formatter.Format(
            GameMessage.Chat(
                GameMessageChannel.Team,
                new MessageSender(
                    new PlayerAddress(IdentityAddress.Encode(new byte[32])),
                    "Account",
                    Guid.NewGuid(),
                    "Lily"
                ),
                "hello"
            )
        );

        Assert.Equal("[队][Lily (Account · scnet1qqqqqqqq…)]hello", formatted.PlainText);
        Assert.Collection(
            formatted.Segments,
            segment => Assert.Equal(MessageTextStyle.Team, segment.Style),
            segment => Assert.Equal(MessageTextStyle.Sender, segment.Style),
            segment => Assert.Equal(MessageTextStyle.Normal, segment.Style)
        );
    }

    [Fact]
    public void CharacterlessAndUnnamedSubjectsHaveAnAddressLabel()
    {
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var message = GameMessage.Chat(GameMessageChannel.Global, new MessageSender(address, string.Empty), "hello");
        Assert.Equal("[scnet1qqqqqqqq…]hello", Game.Localization.ClientLanguage.Formatter.Format(message).PlainText);
    }

    [Fact]
    public void ChatProtocolOnlyExposesGlobalAndTeamChannels()
    {
        Assert.Equal(
            [nameof(GameMessageChannel.Global), nameof(GameMessageChannel.Team)],
            Enum.GetNames<GameMessageChannel>()
        );
    }
}
