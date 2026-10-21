using System.Net;
using System.Net.Sockets;

namespace Engine.Network.Test;

public sealed class CandidatePreparationRaceTest
{
    [Fact]
    public async Task TwoSuccessfulCandidatesReleaseOnlyTheLoserBeforeReturning()
    {
        await using var host = new NetworkHost();
        var released = new List<int>();
        var discarded = new List<int>();
        var first = Preparation(host, () => released.Add(0));
        var second = Preparation(host, () => released.Add(1));
        var selected = await CandidatePreparationRace.SelectAsync(
            [_ => Task.FromResult(first), _ => Task.FromResult(second)],
            discarded.Add,
            CancellationToken.None
        );
        Assert.Same(first, selected);
        Assert.Equal([1], released);
        Assert.Equal([1], discarded);
        Assert.Throws<ObjectDisposedException>(() => second.SendDatagram("closed"u8));
        await selected.DisposeAsync();
        Assert.Equal([1, 0], released);
    }

    [Fact]
    public async Task CancellationAfterBothCandidatesCompleteReleasesBothAndReturnsNoPreparation()
    {
        await using var host = new NetworkHost();
        using var cancellation = new CancellationTokenSource();
        var released = new List<int>();
        var discarded = new List<int>();
        var first = Preparation(host, () => released.Add(0));
        var second = Preparation(host, () => released.Add(1));

        Task<ConnectionPreparation> CompleteAndCancel(CancellationToken token)
        {
            cancellation.Cancel();
            return Task.FromResult(second);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CandidatePreparationRace.SelectAsync(
                [_ => Task.FromResult(first), CompleteAndCancel],
                discarded.Add,
                cancellation.Token
            )
        );
        Assert.Equal([0, 1], released);
        Assert.Equal([0, 1], discarded);
        Assert.Throws<ObjectDisposedException>(() => first.SendDatagram("closed"u8));
        Assert.Throws<ObjectDisposedException>(() => second.SendDatagram("closed"u8));
    }

    [Fact]
    public async Task LosingCandidateThatSucceedsDuringCancellationIsAwaitedAndReleased()
    {
        await using var host = new NetworkHost();
        var released = new List<int>();
        var discarded = new List<int>();
        var first = Preparation(host, () => released.Add(0));
        var second = Preparation(host, () => released.Add(1));
        var late = new TaskCompletionSource<ConnectionPreparation>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<ConnectionPreparation> CompleteDuringCancellation(CancellationToken token)
        {
            token.Register(() => late.TrySetResult(second));
            return late.Task;
        }

        var selected = await CandidatePreparationRace.SelectAsync(
            [_ => Task.FromResult(first), CompleteDuringCancellation],
            discarded.Add,
            CancellationToken.None
        );
        Assert.True(late.Task.IsCompletedSuccessfully);
        Assert.Same(first, selected);
        Assert.Equal([1], released);
        Assert.Equal([1], discarded);
        await selected.DisposeAsync();
    }

    [Fact]
    public async Task CancellationDuringLoserCleanupAlsoReleasesTheSelectedCandidate()
    {
        await using var host = new NetworkHost();
        using var cancellation = new CancellationTokenSource();
        var released = new List<int>();
        var discarded = new List<int>();
        var first = Preparation(host, () => released.Add(0));
        var second = Preparation(
            host,
            () =>
            {
                released.Add(1);
                cancellation.Cancel();
            }
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CandidatePreparationRace.SelectAsync(
                [_ => Task.FromResult(first), _ => Task.FromResult(second)],
                discarded.Add,
                cancellation.Token
            )
        );
        Assert.Equal([1, 0], released);
        Assert.Equal([1, 0], discarded);
        Assert.Throws<ObjectDisposedException>(() => first.SendDatagram("closed"u8));
    }

    [Fact]
    public async Task FailedCandidatesDiscardAllRegistrationsAndKeepTheReportedFailure()
    {
        var discarded = new List<int>();
        var failure =
            new RendezvousException("candidate", RendezvousFailureKind.HolePunchUnavailable, "candidate_timeout");
        var reported = await Assert.ThrowsAsync<RendezvousException>(() => CandidatePreparationRace.SelectAsync(
                [
                    _ => Task.FromException<ConnectionPreparation>(failure),
                    _ => Task.FromException<ConnectionPreparation>(failure)
                ],
                discarded.Add,
                CancellationToken.None
            )
        );
        Assert.Same(failure, reported);
        Assert.Equal([0, 1], discarded);
    }

    [Fact]
    public async Task SocketFailuresAreClassifiedForTheAllowedRelayFallback()
    {
        var discarded = new List<int>();
        var socketFailure = new SocketException((int)SocketError.NetworkUnreachable);
        var reported = await Assert.ThrowsAsync<RendezvousException>(() => CandidatePreparationRace.SelectAsync(
                [
                    _ => Task.FromException<ConnectionPreparation>(socketFailure),
                    _ => Task.FromException<ConnectionPreparation>(socketFailure)
                ],
                discarded.Add,
                CancellationToken.None
            )
        );
        Assert.Equal(RendezvousFailureKind.UdpUnavailable, reported.Kind);
        Assert.Same(socketFailure, reported.InnerException);
        Assert.Equal([0, 1], discarded);
    }

    private static ConnectionPreparation Preparation(NetworkHost host, Action release) =>
        new(
            host,
            new IPEndPoint(IPAddress.Loopback, 30000),
            NetworkPathKind.HolePunch,
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1),
            release,
            TimeProvider.System
        );
}
