using EntitySystem.Core;

using Game.Network;
using Game.Network.Diagnostics;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class PendingNetworkMessagesTest
{
    [Fact]
    public void RejectionPreservesAcceptedWorkAndRemovalOrReplacementReleasesItsCharge()
    {
        using var runtime = new RuntimeScope();
        var original = new OutboundPackage(
            new ModEnvelopePackage("test", "event", new byte[32]),
            PackageAudience.Global,
            runtime.Value
        );
        var charge = ProtocolMessageOwnership.EstimateQueueBytes(original.Package);
        var trackedBytes = 0L;
        var queue = new PendingNetworkMessages(1, charge, change => trackedBytes += change);
        Assert.True(queue.TryEnqueue(original));
        Assert.False(queue.TryEnqueue(original with { Package = new SubsystemTimePackage(1, 0) }));
        Assert.Same(original, Assert.Single(queue));
        Assert.Equal(charge, trackedBytes);
        queue.RemoveAt(0);
        Assert.Equal(0, trackedBytes);
        Assert.True(queue.TryEnqueue(original));
        queue.Replace(0, original with { Package = new ModEnvelopePackage("test", "event", []) });
        Assert.True(trackedBytes < charge);
        Assert.Equal(trackedBytes, queue.PendingBytes);
        queue.Clear();
        Assert.Equal(0, trackedBytes);
        Assert.Empty(queue);
    }

    [Fact]
    public void AnOversizedCoalescedReplacementCannotRetireTheOldMessage()
    {
        using var runtime = new RuntimeScope();
        var statistics = new NetworkSendStatistics();
        var oldFanout = new NetworkMessageFanout(statistics, typeof(OnlineCharacterStatePackage), NetworkChannel.Bulk);
        var old = new OutboundPackage(new OnlineCharacterStatePackage(), PackageAudience.Global, runtime.Value)
            { Fanout = oldFanout };
        var queue = new PendingNetworkMessages(1, ProtocolMessageOwnership.EstimateQueueBytes(old.Package));
        Assert.True(queue.TryEnqueue(old));
        var large = old with { Package = new OnlineCharacterStatePackage(new OnlineCharacterState[10]), Fanout = null };
        Assert.False(queue.TryEnqueue(large));
        Assert.Same(old, Assert.Single(queue));
        Assert.Empty(statistics.GetFanout());
        Assert.True(queue.TryEnqueue(old with { Fanout = null }));
        Assert.Equal(1, Assert.Single(statistics.GetFanout()).Messages);
        Assert.Single(queue);
    }

    [Fact]
    public void ExceedingOneRecipientsCountLimitOnlyRetiresThatRecipientsQueue()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var first = AddClient(node, project, 1);
        var second = AddClient(node, project, 2);
        Assert.True(Queue(node, second, new SubsystemTimePackage(1, 0)));
        for (var index = 0; index < 2048; index++)
        {
            Assert.True(Queue(node, first, new SubsystemTimePackage(index, 0)));
        }

        Assert.False(Queue(node, first, new SubsystemTimePackage(2048, 0)));
        Assert.Equal(1, node.PendingPackageCount);
        Assert.Equal(
            ProtocolMessageOwnership.EstimateQueueBytes(new SubsystemTimePackage(1, 0)),
            node.PendingNetworkPackageBytes
        );
        Assert.False(node.CanReceiveFromSession(first, new SubsystemTimePackage(1, 0)));
        Assert.True(node.CanReceiveFromSession(second, new SubsystemTimePackage(1, 0)));
        Assert.False(Queue(node, first, new SubsystemTimePackage(2049, 0)));
        node.Runtime.Close();
        Assert.Equal(0, node.PendingNetworkPackageBytes);
    }

    [Fact]
    public void AggregateByteLimitRejectsTheNewQueueAndWorldCloseReleasesAllCharges()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var message = new ModEnvelopePackage("test", "event", new byte[15 * 1024 * 1024]);
        for (byte id = 1; id <= 9; id++)
        {
            var client = AddClient(node, project, id);
            Assert.Equal(id <= 8, Queue(node, client, message));
        }

        Assert.Equal(8, node.PendingPackageCount);
        Assert.InRange(node.PendingNetworkPackageBytes, 120L * 1024 * 1024, 128L * 1024 * 1024);
        node.Runtime.Close();
        Assert.Equal(0, node.PendingNetworkPackageBytes);
        Assert.Equal(0, node.PendingPackageCount);
    }

    [Fact]
    public void ConnectionReleaseImmediatelyReclaimsOnlyItsOwnQueueAndIsIdempotent()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var first = AddClient(node, project, 1);
        var second = AddClient(node, project, 2);
        Assert.True(Queue(node, first, new SubsystemTimePackage(1, 0)));
        Assert.True(Queue(node, second, new SubsystemTimePackage(2, 0)));
        var charge = node.PendingNetworkPackageBytes / 2;
        node.ReleaseConnectionDelivery(first);
        node.ReleaseConnectionDelivery(first);
        Assert.Equal(charge, node.PendingNetworkPackageBytes);
        Assert.Equal(1, node.PendingPackageCount);
        node.Runtime.Close();
        Assert.Equal(0, node.PendingNetworkPackageBytes);
    }

    [Fact]
    public void RetryPrecedesLaterEventsAndCannotReplaceANewerQueuedSnapshot()
    {
        using var runtime = new RuntimeScope();
        var queue = new PendingNetworkMessages();

        OutboundPackage Event(double time)
        {
            return new OutboundPackage(new SubsystemTimePackage(time, 0), PackageAudience.Global, runtime.Value);
        }

        Assert.True(queue.TryEnqueue(Event(2)));
        Assert.True(queue.TryEnqueue(Event(3)));
        Assert.True(queue.TryEnqueue(Event(1), prepend: true));
        Assert.Equal(new[] { 1d, 2d, 3d }, queue.Select(item => ((SubsystemTimePackage)item.Package).Time));
        queue.Clear();
        var newer = new OutboundPackage(
            new CharacterStatePackage { ControllerClientId = 7, LadderValue = 2 },
            PackageAudience.Global,
            runtime.Value
        );
        Assert.True(queue.TryEnqueue(newer));
        var charge = queue.PendingBytes;
        Assert.False(
            queue.TryEnqueue(
                newer with { Package = new CharacterStatePackage { ControllerClientId = 7, LadderValue = 1 } },
                prepend: true
            )
        );
        Assert.Same(newer, Assert.Single(queue));
        Assert.Equal(charge, queue.PendingBytes);
    }

    private static Client AddClient(NetNode node, Project project, byte id)
    {
        var client = new Client(null, id, Guid.NewGuid(), Guid.Empty)
        {
            Connection = new ConnectedStub(), State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(client);
        return client;
    }

    private static bool Queue(NetNode node, Client client, IProtocolMessage message)
    {
        var fanout = new NetworkMessageFanout(
            node.SendStatistics,
            message.GetType(),
            PackageTransportPolicy.Get(message).Channel
        );
        var accepted = node.QueueNetworkPackage(
            client,
            new OutboundPackage(message, PackageAudience.To(client), node.Runtime) { Fanout = fanout }
        );
        if (!accepted) { fanout.Advance(1, 0); }

        return accepted;
    }

    private sealed class RuntimeScope : IDisposable
    {
        internal WorldRuntimeContext Value { get; } = new(RuntimeSide.Authority, false);

        public void Dispose() => Value.Close();
    }

    private sealed class ConnectedStub : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => false;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(LocalizedText reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
