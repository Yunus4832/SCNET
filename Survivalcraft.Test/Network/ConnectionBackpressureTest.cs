using System.Reflection;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ConnectionBackpressureTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFullConnectionKeepsItsOwnedMessageAndFanoutUntilDeliveryOrClose(bool close)
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var connection = new BackpressuredConnection();
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            Connection = connection, State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(client);
        node.QueuePackage(new SubsystemTimePackage(7, 0), PackageAudience.To(client));
        var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!;
        flush.Invoke(node, null);
        Assert.Equal(1, node.PendingPackageCount);
        Assert.True(client.IsConnected);
        Assert.Empty(node.SendStatistics.GetFanout());
        if (close) { node.Runtime.Close(); }
        else
        {
            connection.Accept = true;
            flush.Invoke(node, null);
        }

        Assert.Equal(0, node.PendingPackageCount);
        var statistics = Assert.Single(node.SendStatistics.GetFanout());
        Assert.Equal(1, statistics.Messages);
        Assert.Equal(close ? 0 : 1, statistics.RecipientDeliveries);
    }

    [Fact]
    public void BackpressurePreservesChannelOrderWithoutBlockingOtherChannelsOrRecipients()
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
        var blocked = new OrderedConnection();
        var healthy = new OrderedConnection { Accept = true };

        Client AddClient(byte id, IMessageConnection connection)
        {
            var client = new Client(null, id, Guid.NewGuid(), Guid.Empty)
            {
                Connection = connection, State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
            };
            node.AddClient(client);
            return client;
        }

        var first = AddClient(1, blocked);
        AddClient(2, healthy);
        blocked.OnAttempt = () =>
        {
            blocked.Accept = true;
            node.QueuePackage(new SubsystemTimePackage(3, 0), PackageAudience.To(first));
        };
        node.QueuePackage(new SubsystemTimePackage(1, 0), PackageAudience.Global);
        node.QueuePackage(new SubsystemTimePackage(2, 0), PackageAudience.Global);
        node.QueuePackage(new EntityRemovedPackage { EntityId = 7 }, PackageAudience.Global);
        var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!;
        flush.Invoke(node, null);
        Assert.Single(blocked.Attempts.OfType<SubsystemTimePackage>());
        Assert.Single(blocked.Delivered.OfType<EntityRemovedPackage>());
        Assert.Equal(
            new[] { 1d, 2d },
            healthy.Delivered.OfType<SubsystemTimePackage>().Select(message => message.Time)
        );
        Assert.Equal(3, node.PendingPackageCount);

        flush.Invoke(node, null);
        Assert.Equal(
            new[] { 1d, 2d, 3d },
            blocked.Delivered.OfType<SubsystemTimePackage>().Select(message => message.Time)
        );
        Assert.Equal(0, node.PendingPackageCount);
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }

    private sealed class OrderedConnection : IMessageConnection
    {
        public bool Accept { get; set; }

        public Action? OnAttempt { get; set; }

        public List<IProtocolMessage> Attempts { get; } = [];

        public List<IProtocolMessage> Delivered { get; } = [];

        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message)
        {
            Attempts.Add(message.Package);
            var accept = Accept;
            var callback = OnAttempt;
            OnAttempt = null;
            callback?.Invoke();
            if (!accept) { return ConnectionSendResult.Backpressured; }

            Delivered.Add(message.Package);
            return ConnectionSendResult.Accepted;
        }

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class BackpressuredConnection : IMessageConnection
    {
        public bool Accept { get; set; }

        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) =>
            Accept ? ConnectionSendResult.Accepted : ConnectionSendResult.Backpressured;

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
