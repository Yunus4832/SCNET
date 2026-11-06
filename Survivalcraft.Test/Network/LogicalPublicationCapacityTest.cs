using System.Reflection;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Diagnostics;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class LogicalPublicationCapacityTest
{
    [Fact]
    public void BorrowedCapacityCannotBeStolenAndReturningWorkPreservesPublicationOrder()
    {
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var first = Message(runtime, 1);
        var charge = PendingLogicalMessages.Charge(first.Package);
        var queue = new PendingLogicalMessages(2, charge * 2);
        Assert.True(queue.TryEnqueue(first));
        Assert.True(queue.TryEnqueue(Message(runtime, 2)));
        var batch = queue.Borrow(2);
        Assert.Empty(queue);
        Assert.Equal(2, queue.BufferedCount);
        Assert.Equal(charge * 2, queue.PendingBytes);
        Assert.False(queue.TryEnqueue(Message(runtime, 3)));
        queue.Return(batch[1]);
        queue.Return(batch[0]);
        Assert.Equal(new[] { 1d, 2d }, queue.Select(item => ((SubsystemTimePackage)item.Package).Time));
        queue.Discard(runtime);
        queue.Return(batch[0]);
        Assert.Empty(queue);
        Assert.Equal(0, queue.PendingBytes);
        Assert.Equal(0, queue.BufferedCount);
    }

    [Fact]
    public void DiscardingBorrowedWorkCannotRetireFanoutAlreadyTransferredToConnections()
    {
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var statistics = new NetworkSendStatistics();
        var fanout = new NetworkMessageFanout(statistics, typeof(SubsystemTimePackage), NetworkChannel.Control);
        var queue = new PendingLogicalMessages();
        Assert.True(queue.TryEnqueue(Message(runtime, 1) with { Fanout = fanout }));
        var lease = Assert.Single(queue.Borrow(1));
        lease.OwnsFanout = false;
        fanout.Advance(1, 2);
        queue.Discard(runtime);
        Assert.Empty(statistics.GetFanout());
        Assert.Equal(0, queue.PendingBytes);
        fanout.Advance(2, 0);
        Assert.Equal(1, Assert.Single(statistics.GetFanout()).Messages);
    }

    [Fact]
    public void RecipientLimitsAndDiscardDoNotConsumeAnotherRecipientsCredit()
    {
        using var project = new Project();
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var first = new Client(Guid.NewGuid());
        var second = new Client(Guid.NewGuid());
        var queue = new PendingLogicalMessages(recipientCapacity: 1);
        Assert.True(queue.TryEnqueue(Message(runtime, 1) with { Audience = PackageAudience.To(first) }));
        Assert.False(queue.TryEnqueue(Message(runtime, 2) with { Audience = PackageAudience.To(first) }));
        Assert.True(queue.TryEnqueue(Message(runtime, 3) with { Audience = PackageAudience.To(second) }));
        queue.DiscardRecipient(first);
        Assert.Equal(3, Assert.IsType<SubsystemTimePackage>(Assert.Single(queue).Package).Time);
        queue.Discard(runtime);
        Assert.Equal(0, queue.PendingBytes);
    }

    [Fact]
    public void RetriedFanoutStaysAheadOfLaterPublicationsAndBorrowedTail()
    {
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var queue = new PendingLogicalMessages();
        queue.TryEnqueue(Message(runtime, 1));
        queue.TryEnqueue(Message(runtime, 2));
        var batch = queue.Borrow(2);
        queue.TryEnqueue(Message(runtime, 3));
        queue.Release(batch[0]);
        queue.TryEnqueue(Message(runtime, 1), batch[0].Sequence);
        queue.Return(batch[1]);
        Assert.Equal(new[] { 1d, 2d, 3d }, queue.Select(item => ((SubsystemTimePackage)item.Package).Time));
        queue.Discard(runtime);
    }

    [Fact]
    public void SlowLogicalRecipientIsScheduledForClosureWithoutPurgingOtherRecipients()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var slow = AddClient(node, project, 1);
        var healthy = AddClient(node, project, 2);
        node.QueuePackage(new SubsystemTimePackage(9, 0), PackageAudience.To(healthy));
        for (var index = 0; index <= 2048; index++)
        {
            node.QueuePackage(new SubsystemTimePackage(index, 0), PackageAudience.To(slow));
        }

        Assert.Equal(1, node.PendingPackageCount);
        Assert.False(node.IsClosing);
        typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            node,
            null
        );
        Assert.False(node.CanReceiveFromSession(slow, new SubsystemTimePackage(1, 0)));
        Assert.True(node.CanReceiveFromSession(healthy, new SubsystemTimePackage(1, 0)));
        Assert.Equal(0, node.PendingLogicalPackageBytes);
        node.Runtime.Close();
    }

    [Fact]
    public void ByteLimitsRemainReservedUntilBorrowedWorkIsReleased()
    {
        using var project = new Project();
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var first = new Client(Guid.NewGuid());
        var second = new Client(Guid.NewGuid());
        var charge = PendingLogicalMessages.Charge(Message(runtime, 1).Package);
        var queue = new PendingLogicalMessages(capacityBytes: charge * 2, recipientBytes: charge);
        Assert.True(queue.TryEnqueue(Message(runtime, 1) with { Audience = PackageAudience.To(first) }));
        var borrowed = Assert.Single(queue.Borrow(1));
        Assert.False(queue.TryEnqueue(Message(runtime, 2) with { Audience = PackageAudience.To(first) }));
        Assert.True(queue.TryEnqueue(Message(runtime, 3) with { Audience = PackageAudience.To(second) }));
        Assert.False(queue.TryEnqueue(Message(runtime, 4)));
        queue.Release(borrowed);
        Assert.True(queue.TryEnqueue(Message(runtime, 4)));
        queue.Discard(runtime);
        Assert.Equal(0, queue.PendingBytes);
    }

    [Fact]
    public void AggregateProducerOverloadRequestsEndpointShutdownWithoutBlockingPublication()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        for (var value = 0; value <= 8192; value++)
        {
            node.QueuePackage(new SubsystemTimePackage(value, 0), PackageAudience.Global);
        }

        Assert.Equal(8192, node.PendingPackageCount);
        typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            node,
            null
        );
        Assert.True(node.IsClosing);
        node.StopImmediate();
        Assert.Equal(0, node.PendingLogicalPackageBytes);
        Assert.Equal(0, node.PendingPackageCount);
    }

    [Fact]
    public async Task ConcurrentOldWorldPublishersCannotLeakIntoTheEndpointsNextWorld()
    {
        using var oldProject = new Project();
        using var nextProject = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var oldRuntime = node.Runtime;
        oldRuntime.BindProject(oldProject);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishersReady = 0;
        var publishers = Enumerable.Range(0, 4).Select(producer => Task.Run(async () =>
                {
                    for (var index = 0; index < 100; index++)
                    {
                        node.QueuePackage(
                            oldRuntime,
                            new SubsystemTimePackage(producer * 1000 + index, 0),
                            PackageAudience.Global
                        );
                    }

                    if (Interlocked.Increment(ref publishersReady) == 4) { ready.TrySetResult(); }

                    await resume.Task;
                    for (var index = 100; index < 200; index++)
                    {
                        node.QueuePackage(
                            oldRuntime,
                            new SubsystemTimePackage(producer * 1000 + index, 0),
                            PackageAudience.Global
                        );
                    }
                }
            )
        ).ToArray();
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(400, node.PendingPackageCount);
            Assert.Equal(
                PendingLogicalMessages.Charge(new SubsystemTimePackage(0, 0)) * 400,
                node.PendingLogicalPackageBytes
            );
            node.ReleaseProject(oldProject);
            node.PrepareWorldRuntime().BindProject(nextProject);
            resume.TrySetResult();
            await Task.WhenAll(publishers).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(oldRuntime.IsClosed);
            Assert.Equal(0, node.PendingPackageCount);
            Assert.Equal(0, node.PendingLogicalPackageBytes);
            node.QueuePackage(new SubsystemTimePackage(99, 0), PackageAudience.Global);
            Assert.Equal(1, node.PendingPackageCount);
            Assert.Equal(
                PendingLogicalMessages.Charge(new SubsystemTimePackage(99, 0)),
                node.PendingLogicalPackageBytes
            );
        }
        finally
        {
            resume.TrySetResult();
            await Task.WhenAll(publishers).WaitAsync(TimeSpan.FromSeconds(10));
            node.Runtime.Close();
        }
    }

    private static OutboundPackage Message(WorldRuntimeContext runtime, double value)
    {
        return new OutboundPackage(new SubsystemTimePackage(value, 0), PackageAudience.Global, runtime);
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

    private sealed class ConnectedStub : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
