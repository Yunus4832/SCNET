using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ServerSubmissionTest
{
    [Theory]
    [InlineData(NodeRole.Authority)]
    [InlineData(NodeRole.Client)]
    public void InvalidSubmissionCannotFallBackToGlobalBroadcast(NodeRole mode)
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            mode
        );
        node.Server = mode == NodeRole.Client
            ? null
            : new Client(null, 0, Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            NetworkSender.SendToServer(node.Runtime, new CharacterCreateRequestPackage())
        );
    }
}
