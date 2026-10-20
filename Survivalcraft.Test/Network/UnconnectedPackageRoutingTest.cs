using Engine.Network;

using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class UnconnectedPackageRoutingTest
{
    [Fact]
    public async Task MissingDestinationCannotBecomeLanBroadcast()
    {
        await using var network = new NetworkHost();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            network.SendDatagram(null!, NetNode.CreateUnconnectedPayload(new ServerInfoPackage(true))));

        Assert.Equal("endpoint", exception.ParamName);
    }
}
