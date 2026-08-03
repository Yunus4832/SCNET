using Game.Messaging;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class MessagePackageTest
{
    [Fact]
    public void StructuredMessageRoundTrips()
    {
        var package = new MessagePackage(
            new GameMessage(
                GameMessageKind.System,
                GameMessageChannel.Global,
                string.Empty,
                new MessageContent(
                [
                    new MessageSegment("位置："),
                    new MessageSegment(
                        "出生点",
                        MessageTextStyle.Accent)
                ]),
                GameMessageTone.Warning,
                GameMessagePresentation.Default | GameMessagePresentation.Toast)
            {
                LocalizationSection = "MultiplayerUI",
                LocalizationKey = "PlayerJoined",
                LocalizationArguments = ["Lily"]
            });

        var clone = RoundTrip(package);

        Assert.Equal(GameMessageKind.System, clone.GameMessage.Kind);
        Assert.Equal(GameMessageChannel.Global, clone.GameMessage.Channel);
        Assert.Equal(GameMessageTone.Warning, clone.GameMessage.Tone);
        Assert.Equal(
            GameMessagePresentation.Default | GameMessagePresentation.Toast,
            clone.GameMessage.Presentation);
        Assert.Equal("位置：出生点", clone.GameMessage.Content.PlainText);
        Assert.Equal("MultiplayerUI", clone.GameMessage.LocalizationSection);
        Assert.Equal("PlayerJoined", clone.GameMessage.LocalizationKey);
        Assert.Equal(["Lily"], clone.GameMessage.LocalizationArguments);
    }

    private static MessagePackage RoundTrip(MessagePackage package)
    {
        var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new MessagePackage();
        clone.ReadData(reader);
        return clone;
    }
}
