using System.Net;
using System.Net.Sockets;

namespace Engine.Network.Test;

public sealed class PreparationLeaseTest
{
    [Fact]
    public async Task HandshakeLeaseExpiryIsNotReportedAsCallerCancellation()
    {
        using var silent = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var host = new NetworkHost();
        await using var prepared = new ConnectionPreparation(
            host,
            (IPEndPoint)silent.Client.LocalEndPoint!,
            NetworkPathKind.Direct,
            DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(100),
            null,
            TimeProvider.System
        );
        await Assert.ThrowsAsync<TimeoutException>(() =>
            prepared.ConnectAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)
        );
        Assert.Null(host.Failure);
    }

    [Fact]
    public async Task LeaseExpiryWhileWaitingForRenewalGateIsATimeout()
    {
        await using var host = new NetworkHost();
        await using var prepared = new ConnectionPreparation(
            host,
            new IPEndPoint(IPAddress.Loopback, 1),
            NetworkPathKind.Direct,
            DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(100),
            null,
            TimeProvider.System
        );
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelRenewal = new CancellationTokenSource();
        prepared.SetLeaseRenewal(async (_, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DateTimeOffset.UtcNow;
            }
        );
        var renewal = prepared.ExtendLeaseAsync(TimeSpan.FromMinutes(1), cancelRenewal.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<TimeoutException>(() =>
            prepared.ConnectAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)
        );
        cancelRenewal.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renewal);
        Assert.Null(host.Failure);
    }

    [Fact]
    public async Task ConcurrentDisposalsWaitForTheSameRegistrationRelease()
    {
        await using var host = new NetworkHost();
        var releases = 0;
        var prepared = new ConnectionPreparation(
            host,
            new IPEndPoint(IPAddress.Loopback, 1),
            NetworkPathKind.Direct,
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1),
            () => Interlocked.Increment(ref releases),
            TimeProvider.System
        );
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        prepared.SetRegistrationRelease(async token =>
            {
                entered.TrySetResult();
                await finish.Task.WaitAsync(token);
            }
        );
        var first = prepared.DisposeAsync().AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = prepared.DisposeAsync().AsTask();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        finish.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, releases);
    }

    [Fact]
    public async Task RollingLeaseSurvivesOriginalDeadlineButCannotReviveAfterExpiry()
    {
        await using var host = new NetworkHost();
        var clock = new ManualClock();
        await using var prepared = new ConnectionPreparation(
            host,
            new IPEndPoint(IPAddress.Loopback, 1),
            NetworkPathKind.Direct,
            clock.Now + TimeSpan.FromMinutes(1),
            null,
            clock
        );
        clock.Now += TimeSpan.FromSeconds(30);
        await prepared.ExtendLeaseAsync(TimeSpan.FromMinutes(2), CancellationToken.None);
        Assert.Equal(clock.Now + TimeSpan.FromMinutes(2), prepared.ExpiresAt);
        clock.Now += TimeSpan.FromSeconds(90);
        prepared.SendDatagram("still-preparing"u8);
        clock.Now += TimeSpan.FromSeconds(31);
        await Assert.ThrowsAsync<TimeoutException>(() =>
            prepared.ExtendLeaseAsync(TimeSpan.FromMinutes(1), CancellationToken.None)
        );
    }

    [Fact]
    public async Task DisposalCancelsRenewalAndReleasesItsGate()
    {
        await using var host = new NetworkHost();
        var clock = new ManualClock();
        var prepared = new ConnectionPreparation(
            host,
            new IPEndPoint(IPAddress.Loopback, 1),
            NetworkPathKind.Direct,
            clock.Now + TimeSpan.FromMinutes(1),
            null,
            clock
        );
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        prepared.SetLeaseRenewal(async (_, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return clock.Now;
            }
        );
        var renewal = prepared.ExtendLeaseAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await prepared.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renewal);
        Assert.Throws<ObjectDisposedException>(() => prepared.SendDatagram("closed"u8));
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
