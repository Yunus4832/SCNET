using Engine.Network;

using Game.Network;
using Game.Network.Presentation;

namespace Survivalcraft.Test.Network;

public sealed class NetNodeBoundaryTest
{
    [Fact]
    public void MissingRemoteTextKeepsTransportFactUntilClientDisplay()
    {
        var information = new NetworkDisconnectInfo(NetworkDisconnectReason.RemoteClose, ReadOnlyMemory<byte>.Empty, 0);
        var reason = NetNode.DecodeDisconnectReason(information);
        Assert.Empty(reason.Message);
        Assert.Equal(NetworkDisconnectReason.RemoteClose, reason.TransportReason);
        Assert.Equal(
            Game.Localization.ClientLanguage.Catalog.GetContentWidgets("NetPlayScreen", "Disconnect_RemoteClose"),
            DisconnectReasonText.Format(reason)
        );
        Assert.Equal(
            "remote literal",
            DisconnectReasonText.Format(new NodeStopReason("remote literal", NetworkDisconnectReason.RemoteClose))
        );
    }

    [Fact]
    public void ApplicationOwnerCannotSilentlyReplaceItsNode()
    {
        var original = CommonLib.Net;
        Assert.Throws<InvalidOperationException>(() => CommonLib.InitializeNetwork(new NetNodeOptions(1, 1, 1), false));
        Assert.Same(original, CommonLib.Net);
    }
}
