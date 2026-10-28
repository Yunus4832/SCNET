using Game.Managers;
using Game.Network;
using Game.Network.Transport;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class ServerSubmissionTest
{
    [Theory]
    [InlineData(WorldRole.SinglePlayer)]
    [InlineData(WorldRole.Server)]
    [InlineData(WorldRole.RemoteClient)]
    public void InvalidSubmissionCannotFallBackToGlobalBroadcast(WorldRole mode)
    {
        var previousMode = GameManager.Role;
        var previousServer = CommonLib.Net.Server;
        try
        {
            CommonLib.Net.Role = mode;
            CommonLib.Net.Server = mode == WorldRole.RemoteClient
                ? null
                : new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);

            Assert.Throws<InvalidOperationException>(() =>
                NetworkSender.SendToServer(new CharacterCreateRequestPackage())
            );
        }
        finally
        {
            CommonLib.Net.Role = previousMode;
            CommonLib.Net.Server = previousServer;
        }
    }
}
