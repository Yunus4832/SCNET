using EntitySystem.Core;

using Game.Messaging;

namespace Survivalcraft.Test.Messaging;

public sealed class GameMessageServiceTest
{
    [Fact]
    public void PresentationFlagsRouteHistoryOverlayAndToastIndependently()
    {
        using var project = new Project();
        var service = new GameMessageService(project);
        var historyMessages = new List<GameMessage>();
        var overlayMessages = new List<GameMessage>();
        var toastMessages = new List<GameMessage>();
        service.HistoryMessageAdded += historyMessages.Add;
        service.OverlayRequested += overlayMessages.Add;
        service.ToastRequested += toastMessages.Add;

        service.DisplayLocal(GameMessage.System(
            "history",
            presentation: GameMessagePresentation.History));
        service.DisplayLocal(GameMessage.System(
            "overlay",
            presentation: GameMessagePresentation.Overlay));
        service.DisplayLocal(GameMessage.System(
            "toast",
            presentation: GameMessagePresentation.Toast));
        service.DisplayLocal(GameMessage.System("default"));

        Assert.Equal(["history", "default"], historyMessages.Select(MessageText).ToArray());
        Assert.Equal(["overlay", "default"], overlayMessages.Select(MessageText).ToArray());
        Assert.Equal(["toast"], toastMessages.Select(MessageText).ToArray());
        Assert.Equal(["history", "default"], service.History.Select(MessageText).ToArray());
    }

    private static string MessageText(GameMessage message) => message.Content.PlainText;
}
