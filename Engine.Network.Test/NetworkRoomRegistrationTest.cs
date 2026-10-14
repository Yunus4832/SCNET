using Rendezvous.Protocol;

namespace Engine.Network.Test;

public sealed class NetworkRoomRegistrationTest
{
    [Fact]
    public async Task DisposeCancelsPublicationBeforeClosingAndRejectsLaterUpdates()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = false;
        var invitation = new NetworkRoomRegistration(Guid.NewGuid(), "012345", DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5),
            CancellationToken.None, _ => Task.FromResult(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5)),
            _ => { closed = true; return Task.CompletedTask; }, (_, _) => { },
            async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        var publication = new RoomPublication("Room", "", true);
        var updating = invitation.UpdateRoomAsync(publication, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await invitation.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updating);
        Assert.True(closed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => invitation.UpdateRoomAsync(publication, CancellationToken.None));
    }

    [Fact]
    public async Task OfferFailureStopsMaintenanceAndRemainsObservable()
    {
        var failure = new HttpRequestException("Control endpoint unavailable.");
        var notified = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invitation = new NetworkRoomRegistration(Guid.NewGuid(), "012345", DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5),
            CancellationToken.None, _ => Task.FromResult(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5)),
            _ => Task.CompletedTask, (_, exception) => notified.TrySetResult(exception), (_, _) => Task.CompletedTask,
            _ => Task.FromException(failure));
        await invitation.Maintenance.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(failure, invitation.Failure);
        Assert.Same(failure, await notified.Task);
        Assert.False(invitation.IsActive);
        await invitation.DisposeAsync();
    }

    [Fact]
    public async Task RenewalUpdatesExpiryAndDisposeStopsMaintenanceAndClosesOnce()
    {
        var renewedUntil = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10);
        var closed = 0;
        var invitation = new NetworkRoomRegistration(Guid.NewGuid(), "012345", DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5),
            CancellationToken.None, _ => Task.FromResult(renewedUntil), _ =>
            {
                Interlocked.Increment(ref closed);
                return Task.CompletedTask;
            }, (_, _) => throw new InvalidOperationException("Unexpected maintenance failure."), (_, _) => Task.CompletedTask);
        await invitation.RenewAsync(CancellationToken.None);
        Assert.Equal(renewedUntil, invitation.ExpiresAt);
        Assert.True(invitation.IsActive);
        await invitation.DisposeAsync();
        await invitation.DisposeAsync();
        Assert.Equal(1, closed);
        Assert.True(invitation.Maintenance.IsCompletedSuccessfully);
        Assert.False(invitation.IsActive);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => invitation.RenewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisposeCancelsInProgressRenewalBeforeClosing()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = false;
        var invitation = new NetworkRoomRegistration(Guid.NewGuid(), "012345", DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5),
            CancellationToken.None, async token =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DateTimeOffset.UtcNow;
            }, _ =>
            {
                closed = true;
                return Task.CompletedTask;
            }, (_, _) => throw new InvalidOperationException("Unexpected maintenance failure."), (_, _) => Task.CompletedTask);
        var renewing = invitation.RenewAsync(CancellationToken.None);
        await started.Task;
        await invitation.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renewing);
        Assert.True(closed);
    }
}
