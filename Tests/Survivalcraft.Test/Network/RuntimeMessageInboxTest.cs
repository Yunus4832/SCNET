using Engine.Core.Mathematics;

using Game.Network;
using Game.Network.Transport;
using Game.Terrains.Distribution;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class RuntimeMessageInboxTest
{
    [Fact]
    public void CompleteLogicalSnapshotsCanEnterTheInboxWithoutAWireTag()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var context = new PackageReceiveContext(null, runtime, null);
        var snapshot = new ClientChunkSnapshot(
            new ChunkAllocationId(new Point2(4, 5), 1),
            2,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
        var message = new TerrainSnapshotMessage(snapshot);
        Assert.True(WorldNetworkBinding.For(runtime).Inbox.TryPostOwned(new ReceivedPackage(message, context)));
        Assert.Equal(
            1,
            WorldNetworkBinding.For(runtime).Inbox.Drain(
                1,
                received => Assert.Same(message, received.Package)
            )
        );
        Assert.Throws<ArgumentNullException>(() => new TerrainSnapshotMessage(null!));
        var unallocated = new ClientChunkSnapshot(
            new ChunkAllocationId(new Point2(4, 5), 0),
            2,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainSnapshotMessage(unallocated));
    }

    [Fact]
    public void MessagesPostedDuringDeliveryWaitForTheNextPump()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var context = new PackageReceiveContext(null, runtime, null);
        Assert.True(
            WorldNetworkBinding.For(runtime).Inbox
                .TryPostOwned(new ReceivedPackage(new SubsystemTimePackage(1, 0), context))
        );
        var times = new List<double>();

        Assert.Equal(
            1,
            WorldNetworkBinding.For(runtime).Inbox.Drain(
                10,
                message =>
                {
                    times.Add(((SubsystemTimePackage)message.Package).Time);
                    Assert.True(
                        WorldNetworkBinding.For(runtime).Inbox.TryPostOwned(
                            new ReceivedPackage(new SubsystemTimePackage(2, 0), context)
                        )
                    );
                    Assert.Equal(
                        0,
                        WorldNetworkBinding.For(runtime).Inbox.Drain(
                            10,
                            _ => throw new InvalidOperationException("Reentrant delivery.")
                        )
                    );
                }
            )
        );

        Assert.Equal(new[] { 1d }, times);
        Assert.Equal(1, WorldNetworkBinding.For(runtime).Inbox.Count);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(runtime).Inbox.Drain(
                10,
                message => times.Add(((SubsystemTimePackage)message.Package).Time)
            )
        );
        Assert.Equal(new[] { 1d, 2d }, times);
    }

    [Fact]
    public void CapacityRejectionDoesNotDiscardAlreadyAcceptedMessages()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var inbox = new RuntimeMessageInbox(runtime, 1);
        var context = new PackageReceiveContext(null, runtime, null);
        Assert.True(inbox.TryPostOwned(new ReceivedPackage(new SubsystemTimePackage(1, 0), context)));
        Assert.False(inbox.TryPostOwned(new ReceivedPackage(new SubsystemTimePackage(2, 0), context)));
        var delivered = new List<double>();

        Assert.Equal(1, inbox.Drain(1, message => delivered.Add(((SubsystemTimePackage)message.Package).Time)));
        Assert.Equal(new[] { 1d }, delivered);
    }

    [Fact]
    public void ClosingARuntimeDuringDeliveryClearsTheRestOfItsQueue()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var context = new PackageReceiveContext(null, runtime, null);
        var inbox = WorldNetworkBinding.For(runtime).Inbox;
        for (var index = 0; index < 3; index++)
        {
            inbox
                .TryPostOwned(new ReceivedPackage(new SubsystemTimePackage(index, 0), context));
        }

        Assert.Equal(1, inbox.Drain(10, _ => runtime.Close()));
        Assert.Equal(0, inbox.Count);
        Assert.Throws<InvalidOperationException>(() => WorldNetworkBinding.For(runtime));
        Assert.False(
            inbox
                .TryPostOwned(new ReceivedPackage(new SubsystemTimePackage(3, 0), context))
        );
    }

    [Fact]
    public void InboxCannotAcceptMessagesBoundToAnotherRuntime()
    {
        var first = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority);
        var second = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var wrongTarget = new ReceivedPackage(
            new SubsystemTimePackage(1, 0),
            new PackageReceiveContext(null, second, null)
        );

        Assert.Throws<InvalidOperationException>(() => WorldNetworkBinding.For(first).Inbox.TryPostOwned(wrongTarget));
        Assert.Equal(0, WorldNetworkBinding.For(first).Inbox.Count);
        Assert.Equal(0, WorldNetworkBinding.For(second).Inbox.Count);
    }

    [Fact]
    public void InProcessAndEncodedPayloadsUseTheSameProtocolValueChecks()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority);
        var message = new CommandRequestPackage { Payload = new byte[ProtocolLimits.MaximumCommandPayloadLength + 1] };
        var context = new PackageReceiveContext(null, runtime, null);

        Assert.Throws<InvalidDataException>(() => ProtocolPacketCodec.Encode([message]));
        Assert.Throws<InvalidDataException>(() =>
            WorldNetworkBinding.For(runtime).Inbox.TryPostOwned(new ReceivedPackage(message, context))
        );
        Assert.Equal(0, WorldNetworkBinding.For(runtime).Inbox.Count);
    }

    [Fact]
    public void ByteCapacityIsReclaimedWhenCancelledMessagesBlockAdmission()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var context = new PackageReceiveContext(null, runtime, null);
        var message = new SubsystemTimePackage(1, 0);
        var charge = ProtocolMessageOwnership.EstimateQueueBytes(message);
        var inbox = new RuntimeMessageInbox(runtime, capacityBytes: charge);
        using var cancelled = new CancellationTokenSource();
        Assert.True(inbox.TryPostOwned(new ReceivedPackage(message, context), cancelled.Token));
        Assert.Equal(charge, inbox.PendingBytes);
        Assert.False(inbox.TryPostOwned(new ReceivedPackage(message, context)));
        cancelled.Cancel();
        Assert.True(inbox.TryPostOwned(new ReceivedPackage(message, context)));
        Assert.Equal(1, inbox.Count);
        Assert.Equal(charge, inbox.PendingBytes);
        Assert.Equal(1, inbox.Drain(1, _ => { }));
        Assert.Equal(0, inbox.PendingBytes);
    }

    [Fact]
    public void LargeMessagesMakeProgressWithoutExceedingTheNextPumpBudget()
    {
        var runtime = Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client);
        var context = new PackageReceiveContext(null, runtime, null);
        var inbox = new RuntimeMessageInbox(runtime);
        for (var index = 0; index < 2; index++)
        {
            Assert.True(inbox.TryPostOwned(new ReceivedPackage(new SubsystemTimePackage(index, 0), context)));
        }

        Assert.Equal(1, inbox.Drain(10, _ => { }, maximumBytes: 1));
        Assert.Equal(1, inbox.Count);
        Assert.Equal(1, inbox.Drain(10, _ => { }, maximumBytes: 1));
        Assert.Equal(0, inbox.PendingBytes);
    }
}
