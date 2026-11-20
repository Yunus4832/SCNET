using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class InitialWorldSnapshotPhaseTest
{
    [Fact]
    public void InitialSnapshotIsAcceptedOnlyAfterBootstrapAndForTheCurrentNonemptyEpoch()
    {
        var epoch = Guid.NewGuid();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        )
        {
            Role = NodeRole.Client, Server = new Client(null, 0, Guid.NewGuid(), Guid.Empty) { ConnectionEpoch = epoch }
        };
        var package = new InitialWorldSnapshotPackage { Epoch = epoch };
        foreach (var phase in Enum.GetValues<ConnectionPhase>())
        {
            node.Server!.ConnectionPhase = phase;
            Assert.Equal(
                phase == ConnectionPhase.BootstrapApplied,
                InitialWorldSnapshotPackageHandler.CanApply(package, node)
            );
        }

        node.Server!.ConnectionPhase = ConnectionPhase.BootstrapApplied;
        Assert.False(InitialWorldSnapshotPackageHandler.CanApply(package with { Epoch = Guid.NewGuid() }, node));
        node.Server!.ConnectionEpoch = Guid.Empty;
        Assert.False(InitialWorldSnapshotPackageHandler.CanApply(package with { Epoch = Guid.Empty }, node));
        node.Runtime.Close();
    }

    [Fact]
    public void DuplicateSnapshotDoesNotReenterWorldImport()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        )
        {
            Role = NodeRole.Client,
            Server = new Client(null, 0, Guid.NewGuid(), Guid.Empty)
            {
                ConnectionEpoch = Guid.NewGuid(),
                ConnectionPhase = ConnectionPhase.Live
            }
        };
        node.Runtime.BindProject(project);
        var handler = new InitialWorldSnapshotPackageHandler();

        handler.Handle(
            new InitialWorldSnapshotPackage { Epoch = node.Server!.ConnectionEpoch },
            node.CreateReceiveContext()
        );

        Assert.Equal(ConnectionPhase.Live, node.Server!.ConnectionPhase);
        Assert.Equal(0, node.PendingPackageCount);
        node.Runtime.Close();
    }
}
