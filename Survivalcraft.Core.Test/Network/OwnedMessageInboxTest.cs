using Game.Network.Transport;

namespace Survivalcraft.Core.Test.Network;

public sealed class OwnedMessageInboxTest
{
    [Fact]
    public void SenderQueuesRotateAndPreserveTheirOwnOrderAcrossTimeBudgets()
    {
        var first = new object();
        var second = new object();
        var clock = new ManualTimeProvider();
        var released = new List<int>();
        var inbox = new OwnedMessageInbox<int>(() => false, released.Add, timeProvider: clock);
        foreach (var value in new[] { 1, 2, 3 })
        {
            Assert.True(inbox.TryPostOwned(value, first, 1));
        }

        Assert.True(inbox.TryPostOwned(4, second, 1));
        var values = new List<int>();

        void Deliver(int value)
        {
            values.Add(value);
            clock.Advance(TimeSpan.FromMilliseconds(5));
        }

        Assert.Equal(1, inbox.Drain(10, Deliver));
        Assert.Equal(1, inbox.Drain(10, Deliver));
        Assert.Equal(1, inbox.Drain(10, Deliver));
        Assert.Equal(1, inbox.Drain(10, Deliver));
        Assert.Equal(new[] { 1, 4, 2, 3 }, values);
        Assert.Equal(values, released);
        Assert.Equal(0, inbox.PendingBytes);
    }

    [Fact]
    public void FailedDeliveryAndCancellationReleaseOwnedMessagesExactlyOnce()
    {
        var released = new List<int>();
        var inbox = new OwnedMessageInbox<int>(() => false, released.Add, capacityBytes: 2);
        var sender = new object();
        using var cancelled = new CancellationTokenSource();
        Assert.True(inbox.TryPostOwned(1, sender, 1));
        Assert.True(inbox.TryPostOwned(2, sender, 1, cancelled.Token));
        Assert.Throws<InvalidOperationException>(() => inbox.Drain(1, _ => throw new InvalidOperationException()));
        Assert.Equal(new[] { 1 }, released);
        cancelled.Cancel();
        Assert.True(inbox.TryPostOwned(3, sender, 2));
        Assert.Equal(new[] { 1, 2 }, released);
        Assert.False(inbox.TryPostOwned(4, sender, 1));
        inbox.Clear();
        inbox.Clear();
        Assert.Equal(new[] { 1, 2, 3 }, released);
        Assert.Equal(0, inbox.Count);
        Assert.Equal(0, inbox.PendingBytes);
    }

    [Fact]
    public void ClearAttemptsEveryRemovedReleaseAndAggregatesFailuresWithoutRepeatingThem()
    {
        var released = new List<int>();
        var inbox = new OwnedMessageInbox<int>(
            () => false,
            value =>
            {
                released.Add(value);
                if (value != 2) { throw new InvalidOperationException($"release {value}"); }
            }
        );
        var sender = new object();
        foreach (var value in new[] { 1, 2, 3 }) { Assert.True(inbox.TryPostOwned(value, sender, 1)); }

        var error = Assert.Throws<AggregateException>(inbox.Clear);
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal(new[] { 1, 2, 3 }, released);
        Assert.Equal(0, inbox.Count);
        Assert.Equal(0, inbox.PendingBytes);
        inbox.Clear();
        Assert.Equal(3, released.Count);
    }

    [Fact]
    public void PurgeFaultReleasesTheWholeOldBatchBeforeNewMessageOwnershipTransfers()
    {
        var released = new List<int>();
        using var cancelled = new CancellationTokenSource();
        var inbox = new OwnedMessageInbox<int>(
            () => false,
            value =>
            {
                released.Add(value);
                if (value == 1) { throw new InvalidOperationException("release failed"); }
            },
            capacity: 3,
            capacityBytes: 3
        );
        var sender = new object();
        Assert.True(inbox.TryPostOwned(1, sender, 1, cancelled.Token));
        Assert.True(inbox.TryPostOwned(2, sender, 1, cancelled.Token));
        Assert.True(inbox.TryPostOwned(3, sender, 1));
        cancelled.Cancel();

        var error = Assert.Throws<AggregateException>(() => inbox.TryPostOwned(4, sender, 1));
        Assert.Single(error.InnerExceptions);
        Assert.Equal(new[] { 1, 2 }, released);
        Assert.Equal(1, inbox.Count);
        Assert.Equal(1, inbox.PendingBytes);
        // The throwing call never accepted 4, so its caller may safely retry the same message.
        Assert.True(inbox.TryPostOwned(4, sender, 1));
        var delivered = new List<int>();
        Assert.Equal(2, inbox.Drain(10, delivered.Add, maximumTime: TimeSpan.FromSeconds(1)));
        Assert.Equal(new[] { 3, 4 }, delivered);
        Assert.Equal(new[] { 1, 2, 3, 4 }, released);
        inbox.Clear();
        Assert.Equal(4, released.Count);
    }

    [Fact]
    public void ReentrantReleaseCanConsumeFreedCapacityWithoutFalselyAcceptingTheOuterMessage()
    {
        var released = new List<int>();
        using var cancelled = new CancellationTokenSource();
        var sender = new object();
        OwnedMessageInbox<int>? inbox = null;
        inbox = new OwnedMessageInbox<int>(
            () => false,
            value =>
            {
                released.Add(value);
                if (value == 1) { Assert.True(inbox!.TryPostOwned(99, sender, 1)); }
            },
            capacity: 1
        );
        Assert.True(inbox.TryPostOwned(1, sender, 1, cancelled.Token));
        cancelled.Cancel();

        Assert.False(inbox.TryPostOwned(2, sender, 1));
        var delivered = new List<int>();
        Assert.Equal(1, inbox.Drain(10, delivered.Add));
        Assert.Equal(new[] { 99 }, delivered);
        Assert.Equal(new[] { 1, 99 }, released);
    }

    [Fact]
    public void OnePostDoesNotRepeatedlyPurgeReentrantCancelledReplacementWork()
    {
        using var oldConnection = new CancellationTokenSource();
        using var replacementConnection = new CancellationTokenSource();
        var released = new List<int>();
        var sender = new object();
        OwnedMessageInbox<int>? inbox = null;
        inbox = new OwnedMessageInbox<int>(
            () => false,
            value =>
            {
                released.Add(value);
                if (value != 1) { return; }

                Assert.True(inbox!.TryPostOwned(99, sender, 1, replacementConnection.Token));
                replacementConnection.Cancel();
            },
            capacity: 1
        );
        Assert.True(inbox.TryPostOwned(1, sender, 1, oldConnection.Token));
        oldConnection.Cancel();

        Assert.False(inbox.TryPostOwned(2, sender, 1));
        Assert.Equal(new[] { 1 }, released);
        Assert.Equal(1, inbox.Count);
        inbox.Clear();
        Assert.Equal(new[] { 1, 99 }, released);
        Assert.True(inbox.TryPostOwned(2, sender, 1));
        inbox.Clear();
        Assert.Equal(new[] { 1, 99, 2 }, released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StateAndIncomingConnectionAreRecheckedAfterPurgeRelease(bool cancelConnection)
    {
        using var oldConnection = new CancellationTokenSource();
        using var newConnection = new CancellationTokenSource();
        var closed = false;
        var released = new List<int>();
        var inbox = new OwnedMessageInbox<int>(
            () => closed,
            value =>
            {
                released.Add(value);
                if (cancelConnection) { newConnection.Cancel(); }
                else { closed = true; }
            },
            capacity: 1
        );
        var sender = new object();
        Assert.True(inbox.TryPostOwned(1, sender, 1, oldConnection.Token));
        oldConnection.Cancel();

        Assert.False(inbox.TryPostOwned(2, sender, 1, newConnection.Token));
        Assert.Equal(0, inbox.Count);
        Assert.Equal(0, inbox.PendingBytes);
        Assert.Equal(new[] { 1 }, released);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        internal void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
