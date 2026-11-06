using Game.Protocol.Messaging;
using Game.Protocol.Serialization;

namespace Game.Protocol.Test;

public sealed class ToastStyleCodecTest
{
    [Fact]
    public void ExplicitToastStyleRoundTripsWithoutChangingMessageToneOrPresentation()
    {
        var original = GameMessage.System(
                "limit",
                GameMessageTone.Warning,
                GameMessagePresentation.History | GameMessagePresentation.Toast
            ) with
            {
                ToastStyle = new GameMessageToastStyle(GameMessageTone.Normal, true, false)
            };
        var decoded = Assert.IsType<MessagePackage>(
            ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(new MessagePackage(original)))
        ).GameMessage;

        Assert.Equal(original.ToastStyle, decoded.ToastStyle);
        Assert.Equal(GameMessageTone.Warning, decoded.Tone);
        Assert.Equal(original.Presentation, decoded.Presentation);
        Assert.Equal("limit", decoded.Content.PlainText);
        Assert.Null(decoded.Sender);
    }

    [Fact]
    public void OrdinaryMessagesKeepTheDefaultUnspecifiedToastStyle()
    {
        var decoded = Assert.IsType<MessagePackage>(
            ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(new MessagePackage(GameMessage.System("default"))))
        );
        Assert.Null(decoded.GameMessage.ToastStyle);
    }

    [Fact]
    public void UndefinedToastToneIsRejectedBeforePublication()
    {
        var message = GameMessage.System("invalid") with
        {
            ToastStyle = new GameMessageToastStyle((GameMessageTone)byte.MaxValue, true, false)
        };
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Encode(new MessagePackage(message)));
    }
}
