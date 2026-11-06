using System.Reflection;

using Game.Blocks;
using Game.Components;
using Game.Messaging;
using Game.Network;
using Game.Network.Transport;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Terrains;

public sealed class BlockPlacementFeedbackTest
{
    [Fact]
    public void RejectedPredictionReturnsFalseWithOneLocalToastAndNoNetworkRequest()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var messages = new GameMessageService(project);
        var toasts = 0;
        messages.ToastRequested += _ => toasts++;
        Assert.False(ComponentMiner.RejectPlacement(messages, false, true, 2, BlockPlacementRejection.MagnetLimit));
        Assert.Equal(1, toasts);
        Assert.Equal(0, WorldNetworkBinding.For(project).RequireNode().PendingPackageCount);
    }

    [Fact]
    public void AuthorityRejectionQueuesOnlyOneToastToTheRequestingClient()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).RequireNode();
        var owner = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid());
        var other = new Client(null, 3, Guid.NewGuid(), Guid.NewGuid());
        node.AddClient(owner);
        node.AddClient(other);
        var messages = new GameMessageService(project);
        var localToasts = 0;
        messages.ToastRequested += _ => localToasts++;
        Assert.False(
            ComponentMiner.RejectPlacement(messages, true, false, owner.ID, BlockPlacementRejection.MagnetLimit)
        );
        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(node)!;
        var queued = Assert.Single(pending);
        Assert.IsType<MessagePackage>(queued.Package);
        Assert.True(queued.Audience.Includes(owner));
        Assert.False(queued.Audience.Includes(other));
        Assert.Equal(0, localToasts);
        Assert.Empty(messages.History);
    }
}
