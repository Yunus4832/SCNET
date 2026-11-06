using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class WorldConnectionRegistrationTest
{
    [Fact]
    public void PhysicalAndLogicalOwnersRegisterTheSameRecordAndRemoveOnlyThatInstance()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        try
        {
            var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
            node.AddClient(first);
            Assert.Same(first, node.Connections.Members[1]);
            Assert.Same(first, node.RequirePhysicalConnection(first));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<int, Client>)node.Clients).Clear());
            var audience = PackageAudience.To(first);
            var replacement = new Client(null, 1, Guid.NewGuid(), first.CharacterId);
            node.AddClient(replacement);

            Assert.Same(replacement, node.Clients[1]);
            Assert.Same(replacement, node.Connections.Members[1]);
            Assert.False(node.UnregisterClient(first));
            Assert.False(audience.Includes(replacement));
            Assert.Throws<InvalidOperationException>(() => node.RequirePhysicalConnection(first));
            Assert.True(node.UnregisterClient(replacement));
            Assert.Empty(node.Clients);
            Assert.Empty(node.Connections.Members);
        }
        finally { node.StopImmediate(); }
    }

    [Fact]
    public void FailedCrossOwnerRegistrationCannotRetireAnExistingRecipient()
    {
        var first = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        var second = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        try
        {
            var existing = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
            var foreign = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
            first.AddClient(existing);
            second.AddClient(foreign);

            Assert.Throws<InvalidOperationException>(() => first.AddClient(foreign));
            Assert.Same(existing, first.Clients[1]);
            Assert.Same(existing, first.Connections.Members[1]);
            Assert.Same(foreign, second.Connections.Members[1]);
        }
        finally
        {
            first.StopImmediate();
            second.StopImmediate();
        }
    }

    [Fact]
    public void NewWorldCanReuseTransportButNotOldLogicalRecipientOrQueuedMessages()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        try
        {
            var transport = new ConnectedTransport();
            var oldConnection = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid()) { Connection = transport };
            node.AddClient(oldConnection);
            var oldWorld = node.Runtime;
            var oldInbox = WorldNetworkBinding.For(oldWorld).Inbox;
            var context = node.CreateReceiveContext(oldConnection);
            var message = new SubsystemTimePackage(1, 0);
            Assert.True(oldInbox.TryPostOwned(new ReceivedPackage(message, context)));
            var oldAudience = PackageAudience.To(oldConnection);

            node.Role = NodeRole.Authority;
            Assert.True(oldWorld.IsClosed);
            Assert.Empty(node.Clients);
            Assert.Equal(0, oldInbox.Count);
            Assert.False(oldInbox.TryPostOwned(new ReceivedPackage(message, context)));
            node.QueuePackage(oldWorld, message, PackageAudience.Global);
            Assert.Equal(0, node.PendingPackageCount);
            Assert.Throws<InvalidOperationException>(() => node.AddClient(oldConnection));
            var next = new Client(null, 1, Guid.NewGuid(), oldConnection.CharacterId) { Connection = transport };
            node.AddClient(next);
            Assert.Same(next, node.Connections.Members[1]);
            Assert.Same(next, node.RequirePhysicalConnection(next));
            Assert.False(oldAudience.Includes(next));
            Assert.True(next.IsConnected);
        }
        finally { node.StopImmediate(); }
    }

    private sealed class ConnectedTransport : IMessageConnection
    {
        public bool IsConnected { get; private set; } = true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => throw new NotSupportedException();

        public void Disconnect(string reason)
        {
            IsConnected = false;
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
