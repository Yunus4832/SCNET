using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Messaging;
using Game.Widgets;

namespace Survivalcraft.Test.Messaging;

public sealed class ToastStyleTest
{
    [Theory]
    [InlineData(GameMessageKind.System, GameMessageTone.Normal, false)]
    [InlineData(GameMessageKind.System, GameMessageTone.Success, false)]
    [InlineData(GameMessageKind.System, GameMessageTone.Warning, true)]
    [InlineData(GameMessageKind.System, GameMessageTone.Error, true)]
    [InlineData(GameMessageKind.Sign, GameMessageTone.Normal, true)]
    public void OrdinaryMessagesKeepTheirExistingToastDefaults(
        GameMessageKind kind,
        GameMessageTone tone,
        bool blinking
    )
    {
        var message = GameMessage.System("message", tone) with { Kind = kind };
        var style = GameWidget.ResolveToastStyle(message);
        Assert.Equal(tone, style.Tone);
        Assert.Equal(blinking, style.Blinking);
        Assert.Equal(blinking, style.PlayNotificationSound);
    }

    [Fact]
    public void WhiteBlinkingSilentToastDoesNotChangeHistoryToneOrFlags()
    {
        var style = new GameMessageToastStyle(GameMessageTone.Normal, true, false);
        var message =
            GameMessage.System("warning history", GameMessageTone.Warning, GameMessagePresentation.History) with
            {
                ToastStyle = style
            };
        Assert.Equal(style, GameWidget.ResolveToastStyle(message));
        Assert.Equal(Color.White, GameWidget.ResolveToastColor(style));
        using var project = new Project();
        var messages = new GameMessageService(project);
        var toastCount = 0;
        messages.ToastRequested += _ => toastCount++;

        messages.DisplayLocal(message);

        Assert.Equal(GameMessageTone.Warning, Assert.Single(messages.History).Tone);
        Assert.Equal(GameMessagePresentation.History, Assert.Single(messages.History).Presentation);
        Assert.Equal(0, toastCount);
    }
}
