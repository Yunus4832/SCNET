using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class NetNodeLifecycleTest
{
    [Fact]
    public void StoppedNodeDoesNotRetainClientsOrTheirWorldReferences()
    {
        var previousMode = CommonLib.WorkType;
        CommonLib.WorkType = WorkType.Local;
        try
        {
            using var project = new Project();
            var node = new NetNode();
            var client = new Client(Guid.NewGuid(), project);
            node.Clients.Add(0, client);
            node.Self = client;
            node.Server = client;
            node.StopImmediate();
            node.StopImmediate();
            node.QueuePackage(new ConnectionPhaseAckPackage(Guid.Empty, ConnectionPhase.TransportConnected), PackageAudience.Global);
            Assert.Equal(0, node.PendingPackageCount);
            Assert.Empty(node.Clients);
            Assert.Null(node.Self);
            Assert.Null(node.Server);
        }
        finally
        {
            CommonLib.WorkType = previousMode;
        }
    }
}
