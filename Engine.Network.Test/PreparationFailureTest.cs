using System.Net;
using System.Net.Http.Json;

using Engine.Network.Rendezvous;

using Rendezvous.Protocol;

namespace Engine.Network.Test;

public sealed class PreparationFailureTest
{
    [Fact]
    public async Task ConfiguredPreparationDeadlineCancelsControlRequestAndLeavesNoPaths()
    {
        using var handler = new StalledControlHandler();
        using var http = new HttpClient(handler);
        await using var host = new NetworkHost(
            options: new NetworkHostOptions
            {
                PreparationTimeout = TimeSpan.FromMilliseconds(100)
            }
        );
        var preparing = host.PrepareConnectionAsync(
            new InvitationCodeNetworkTarget(new Uri("http://localhost/"), "123456"),
            http,
            CancellationToken.None
        );
        var failure = await Assert.ThrowsAsync<RendezvousException>(() => preparing.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(RendezvousFailureKind.Timeout, failure.Kind);
        Assert.Equal("preparation_timeout", failure.ErrorCode);
        Assert.True(handler.Cancelled);
        Assert.Equal(0, host.Resources.PendingSignals);
        Assert.Equal(0, host.Resources.CandidatePaths);
        Assert.Equal(0, host.Resources.RelayRoutes);
        Assert.Null(host.Failure);
    }

    private sealed class StalledControlHandler : HttpMessageHandler
    {
        public bool Cancelled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Stalled request unexpectedly resumed.");
            }
            finally
            {
                Cancelled = cancellationToken.IsCancellationRequested;
            }
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 2)]
    public async Task InvalidRelayCredentialsAreRejectedBeforeInstallation(int keyLength, byte side)
    {
        await using var host = new NetworkHost();
        var allocation = new RelayAllocation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "127.0.0.1:30000",
            new RelayRouteCredentials(Guid.NewGuid(), side, new byte[keyLength]),
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1)
        );
        var failure = Assert.Throws<RendezvousException>(() => host.TryInstallRelay(allocation));
        Assert.Equal(RendezvousFailureKind.InvalidResponse, failure.Kind);
        Assert.Equal("relay/install", failure.Operation);
        Assert.Null(host.Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidAllocationRemainsTheReportedFailureWhenRegistrationReleaseFails(bool releaseFails)
    {
        var participant = new ParticipantRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fixture-participant",
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1)
        );
        using var handler = new AllocationHandler(participant, releaseFails);
        using var http = new HttpClient(handler);
        await using var host = new NetworkHost();
        var signaling = new RendezvousClient(http, new Uri("http://localhost/"));
        var failure = await Assert.ThrowsAsync<RendezvousException>(() => host.PrepareInvitationAsync(
                signaling,
                [new IPEndPoint(IPAddress.Loopback, 1)],
                "123456",
                RelayPolicy.Required,
                CancellationToken.None
            )
        );
        Assert.Equal(RendezvousFailureKind.InvalidResponse, failure.Kind);
        Assert.Equal("relay/allocate", failure.Operation);
        Assert.Equal("invalid_client_relay_allocation", failure.ErrorCode);
        Assert.Equal(1, handler.ReleaseRequests);
        Assert.Null(host.Failure);
    }

    private sealed class AllocationHandler(ParticipantRegistration participant, bool releaseFails) : HttpMessageHandler
    {
        public int ReleaseRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = request.RequestUri!.AbsolutePath switch
            {
                "/invitations/join" => new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(participant) },
                "/relay/allocate" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new RelayAllocation(
                            Guid.NewGuid(),
                            participant.ParticipantId,
                            Guid.NewGuid(),
                            "127.0.0.1:1",
                            new RelayRouteCredentials(Guid.NewGuid(), 1, new byte[32]),
                            participant.ExpiresAt
                        )
                    )
                },
                "/invitations/leave" => Release(),
                _ => throw new InvalidOperationException("Unexpected fixture request.")
            };
            return Task.FromResult(response);
        }

        private HttpResponseMessage Release()
        {
            ReleaseRequests++;
            return new HttpResponseMessage(releaseFails ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent);
        }
    }
}
