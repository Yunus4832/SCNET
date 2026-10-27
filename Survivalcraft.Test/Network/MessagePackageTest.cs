using Engine.Core.Mathematics;
using Engine.Core.Security;

using Game.Protocol.Identity;

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
                null,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChatSenderRoundTripsSubjectAndOptionalCharacterSnapshot(bool withCharacter)
    {
        var sender = new MessageSender(
            new PlayerAddress(IdentityAddress.Encode(new byte[32])),
            "Account",
            withCharacter ? Guid.NewGuid() : null,
            withCharacter ? "Character at send time" : string.Empty
        );
        var clone = RoundTrip(new MessagePackage(GameMessage.Chat(GameMessageChannel.Global, sender, "hello")));
        Assert.Equal(sender, clone.GameMessage.Sender);
    }

    [Theory]
    [InlineData(GameMessageKind.Chat, false)]
    [InlineData(GameMessageKind.System, true)]
    public void DecoderRejectsMissingOrInappropriateSender(GameMessageKind kind, bool hasSender)
    {
        var sender = hasSender
            ? new MessageSender(new PlayerAddress(IdentityAddress.Encode(new byte[32])), "Account")
            : null;
        var invalid = new MessagePackage(
            new GameMessage(kind, GameMessageChannel.Global, sender, MessageContent.Plain("test"))
        );
        var payload = ProtocolPayloadCodec.Encode<IProtocolMessage>(invalid);
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(payload));
    }

    [Fact]
    public void EncoderRejectsCharacterNameWithoutACharacterAndEmptyCharacterIds()
    {
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        foreach (var sender in new[]
                 {
                     new MessageSender(address, "Account", null, "Fake role"),
                     new MessageSender(address, "Account", Guid.Empty)
                 })
        {
            Assert.Throws<InvalidDataException>(() =>
                ProtocolMessageCodec.Encode(
                    new MessagePackage(GameMessage.Chat(GameMessageChannel.Global, sender, "hello"))
                )
            );
        }
    }

    private static MessagePackage RoundTrip(MessagePackage package)
    {
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<MessagePackage>(ProtocolMessageCodec.Decode(reader));
        return clone;
    }
}
