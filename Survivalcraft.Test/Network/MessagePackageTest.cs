using Engine.Core;

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
                            MessageTextStyle.Accent,
                            new Color(12, 34, 56, 78)
                        )
                    ]
                ),
                GameMessageTone.Warning,
                GameMessagePresentation.Default | GameMessagePresentation.Toast
            )
            {
                LocalizationSection = "MultiplayerUI",
                LocalizationKey = "PlayerJoined",
                LocalizationArguments = ["Lily"]
            }
        );

        var clone = RoundTrip(package);

        Assert.Equal(GameMessageKind.System, clone.GameMessage.Kind);
        Assert.Equal(GameMessageChannel.Global, clone.GameMessage.Channel);
        Assert.Equal(GameMessageTone.Warning, clone.GameMessage.Tone);
        Assert.Equal(
            GameMessagePresentation.Default | GameMessagePresentation.Toast,
            clone.GameMessage.Presentation
        );
        Assert.Equal("位置：出生点", clone.GameMessage.Content.PlainText);
        Assert.Null(clone.GameMessage.Content.Segments[0].Color);
        Assert.Equal(new Color(12, 34, 56, 78), clone.GameMessage.Content.Segments[1].Color);
        Assert.Equal("MultiplayerUI", clone.GameMessage.LocalizationSection);
        Assert.Equal("PlayerJoined", clone.GameMessage.LocalizationKey);
        Assert.Equal(["Lily"], clone.GameMessage.LocalizationArguments);
    }

    private static MessagePackage RoundTrip(MessagePackage package)
    {
        var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        var clone = Assert.IsType<MessagePackage>(ProtocolMessageCodec.Read(package.ID, reader));
        return clone;
    }
}
