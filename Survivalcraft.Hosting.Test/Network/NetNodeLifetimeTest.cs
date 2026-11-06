using System.Reflection;

using Engine.Network;

using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class NetNodeLifetimeTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StopCancelsPendingConnectionAndResetsEvenWhenPathReleaseFails(bool releaseFails)
    {
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Client);
        var runtime = node.Runtime;
        using var cancellation = new CancellationTokenSource();
        var connecting =
            new TaskCompletionSource<NetworkConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() => connecting.TrySetCanceled(cancellation.Token));
        SetField(node, "_connectingCancellation", cancellation);
        SetField(node, "_connecting", connecting.Task);
        SetField(
            node,
            "_pathClosing",
            releaseFails ? Task.FromException(new IOException("Release failed.")) : Task.CompletedTask
        );

        node.StopImmediate();

        Assert.True(connecting.Task.IsCanceled);
        Assert.True(runtime.IsClosed);
        Assert.Equal(NodeRole.Inactive, node.Role);
        Assert.Equal(NetNode.Stage.NotConnected, node.CurrentStage);
        Assert.Null(GetField(node, "_connecting"));
        Assert.Null(GetField(node, "_pathClosing"));
        Assert.Null(GetField(node, "_connectingCancellation"));
        node.StopImmediate();
        Assert.Equal(NodeRole.Inactive, node.Role);
    }

    private static void SetField(NetNode node, string name, object value)
    {
        typeof(NetNode).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(node, value);
    }

    private static object? GetField(NetNode node, string name)
    {
        return typeof(NetNode).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node);
    }
}
