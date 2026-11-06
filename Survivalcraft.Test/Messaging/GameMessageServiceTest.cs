using EntitySystem.Core;

using Game.Messaging;
using Game.World;

using Survivalcraft.Test.Fixtures;

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

        service.DisplayLocal(
            GameMessage.System(
                "history",
                presentation: GameMessagePresentation.History
            )
        );
        service.DisplayLocal(
            GameMessage.System(
                "overlay",
                presentation: GameMessagePresentation.Overlay
            )
        );
        service.DisplayLocal(
            GameMessage.System(
                "toast",
                presentation: GameMessagePresentation.Toast
            )
        );
        service.DisplayLocal(GameMessage.System("default"));

        Assert.Equal(["history", "default"], historyMessages.Select(MessageText).ToArray());
        Assert.Equal(["overlay", "default"], overlayMessages.Select(MessageText).ToArray());
        Assert.Equal(["toast"], toastMessages.Select(MessageText).ToArray());
        Assert.Equal(["history", "default"], service.History.Select(MessageText).ToArray());
    }

    [Fact]
    public void PublishingUsesItsOwnWorldAndStopsWhenThatWorldCloses()
    {
        using var first = new RuntimeTestProject(RuntimeSide.Authority);
        using var second = new RuntimeTestProject(RuntimeSide.Authority);
        var firstNode = Game.Network.WorldNetworkBinding.For(first).RequireNode();
        var secondNode = Game.Network.WorldNetworkBinding.For(second).RequireNode();
        var firstMessages = new GameMessageService(first);
        var secondMessages = new GameMessageService(second);

        firstMessages.Publish(GameMessage.System("first"));
        Assert.Equal(1, firstNode.PendingPackageCount);
        Assert.Equal(0, secondNode.PendingPackageCount);
        secondMessages.Publish(GameMessage.System("second"));
        Assert.Equal(1, secondNode.PendingPackageCount);
        firstNode.Update();
        Assert.Equal(0, firstNode.PendingPackageCount);
        Assert.Equal(1, secondNode.PendingPackageCount);

        firstNode.Runtime.Close();
        firstNode.PrepareWorldRuntime();
        firstMessages.Publish(GameMessage.System("closed"));
        Assert.Equal(0, firstNode.PendingPackageCount);
        Assert.Equal("first", MessageText(Assert.Single(firstMessages.History)));
        Assert.Equal("second", MessageText(Assert.Single(secondMessages.History)));
        first.Dispose();
        firstMessages.Publish(GameMessage.System("disposed"));
        Assert.Equal(0, firstNode.PendingPackageCount);
        firstNode.Runtime.Close();
    }

    private static string MessageText(GameMessage message) => message.Content.PlainText;
}
