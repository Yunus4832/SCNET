using Game.Network;
using Game.Network.Packages;

using LiteNetLib;

namespace Survivalcraft.Test.Network;

public sealed class UnconnectedPackageRoutingTest
{
    [Fact]
    public void MissingDestinationCannotBecomeLanBroadcast()
    {
        var network = new NetManager(new EventBasedNetListener());

        var exception = Assert.Throws<ArgumentNullException>(() =>
            NetNode.SendUnconnectedPackage(network, new ServerInfoPackage(true), null!));

        Assert.Equal("endPoint", exception.ParamName);
    }
}
