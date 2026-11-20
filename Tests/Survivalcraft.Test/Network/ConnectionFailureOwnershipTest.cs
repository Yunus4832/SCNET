using System.Reflection;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ConnectionFailureOwnershipTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailureRetiresOnlyUntransferredWorkAndReturnsTheBorrowedTail(bool ioFailure)
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority,
            new FrozenTimeProvider()
        );
        node.Runtime.BindProject(project);
        Add(node, project, 1, new TestConnection { Backpressured = true });
        var failed = Add(
            node,
            project,
            2,
            new TestConnection
            {
                Failure = ioFailure
                    ? new IOException("Simulated transport failure.")
                    : new InvalidOperationException("Simulated adapter defect.")
            }
        );
        var goodConnection = new TestConnection();
        var healthy = Add(node, project, 3, goodConnection);
        node.QueuePackage(new SubsystemTimePackage(1, 0), PackageAudience.Global);
        node.QueuePackage(new SubsystemTimePackage(2, 0), PackageAudience.To(healthy));
        var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.NonPublic | BindingFlags.Instance)!;
        if (ioFailure)
        {
            flush.Invoke(node, null);
            Assert.False(node.CanReceiveFromSession(failed, new SubsystemTimePackage(1, 0)));
            Assert.Equal(new[] { 1d, 2d }, goodConnection.Delivered);
        }
        else
        {
            var exception = Assert.Throws<TargetInvocationException>(() => flush.Invoke(node, null));
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Empty(goodConnection.Delivered);
            Assert.Equal(1, node.PendingPackageCount);
            flush.Invoke(node, null);
            Assert.Equal(new[] { 2d }, goodConnection.Delivered);
        }

        node.Runtime.Close();
        Assert.Equal(0, node.PendingLogicalPackageBytes);
        Assert.Equal(0, node.PendingPackageCount);
        var fanout = Assert.Single(node.SendStatistics.GetFanout());
        Assert.Equal(2, fanout.Messages);
        Assert.Equal(ioFailure ? 2 : 1, fanout.RecipientDeliveries);
    }

    private static Client Add(NetNode node, Project project, byte id, IMessageConnection connection)
    {
        var client = new Client(null, id, Guid.NewGuid(), Guid.Empty)
        {
            Connection = connection, State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(client);
        return client;
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }

    private sealed class TestConnection : IMessageConnection
    {
        internal Exception? Failure { get; init; }

        internal bool Backpressured { get; init; }

        internal List<double> Delivered { get; } = [];

        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message)
        {
            if (Failure is not null) { throw Failure; }

            if (Backpressured) { return ConnectionSendResult.Backpressured; }

            Delivered.Add(((SubsystemTimePackage)message.Package).Time);
            return ConnectionSendResult.Accepted;
        }

        public void Disconnect(LocalizedText reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
