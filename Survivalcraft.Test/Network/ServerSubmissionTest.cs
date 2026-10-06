using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class ServerSubmissionTest
{
    [Theory]
    [InlineData(WorkType.Local)]
    [InlineData(WorkType.Server)]
    [InlineData(WorkType.Client)]
    public void InvalidSubmissionCannotFallBackToGlobalBroadcast(WorkType mode)
    {
        var previousMode = CommonLib.WorkType;
        var previousServer = CommonLib.Net.Server;
        try
        {
            CommonLib.WorkType = mode;
            CommonLib.Net.Server = mode == WorkType.Client
                ? null
                : new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);

            Assert.Throws<InvalidOperationException>(() => NetworkSender.SendToServer(new PlayerDataPackage()));
        }
        finally
        {
            CommonLib.WorkType = previousMode;
            CommonLib.Net.Server = previousServer;
        }
    }
}
