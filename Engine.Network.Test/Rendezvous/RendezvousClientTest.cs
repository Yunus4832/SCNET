using System.Net;
using System.Net.Http.Json;

using Engine.Network.Rendezvous;

using Rendezvous.Protocol;

namespace Engine.Network.Test.Rendezvous;

public sealed class RendezvousClientTest
{
    [Fact]
    public async Task RoomDirectoryRejectsOversizedAndInvalidSuccessfulResponses()
    {
        foreach (var content in new HttpContent[]
                 {
                     new StringContent(new string('x', 1024 * 1024 + 1)),
                     JsonContent.Create(
                         new RoomPage([new RoomSummary(Guid.NewGuid(), "Room", "", "12", ["holePunch", "relay"])], null)
                     )
                 })
        {
            using var handler = new ResponseHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content })
            );
            using var http = new HttpClient(handler);
            var client = new RendezvousClient(http, new Uri("http://localhost/"));
            var failure =
                await Assert.ThrowsAsync<RendezvousException>(() =>
                    client.GetRoomsAsync(null, 100, CancellationToken.None)
                );
            Assert.Equal(RendezvousFailureKind.InvalidResponse, failure.Kind);
        }
    }

    [Theory]
    [InlineData("invitation_not_found", RendezvousFailureKind.InvitationNotFound)]
    [InlineData("invitation_full", RendezvousFailureKind.InvitationFull)]
    [InlineData("invalid_join_code", RendezvousFailureKind.InvalidRequest)]
    [InlineData("relay_capacity_reached", RendezvousFailureKind.RelayUnavailable)]
    [InlineData("unauthorized_participant", RendezvousFailureKind.Unauthorized)]
    [InlineData("invitation_capacity_reached", RendezvousFailureKind.CapacityReached)]
    public async Task PreservesServerErrorCodeAndOperation(string code, RendezvousFailureKind expected)
    {
        using var handler = new ResponseHandler(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new RendezvousErrorResponse(code))
                }
            )
        );
        using var http = new HttpClient(handler);
        var client = new RendezvousClient(http, new Uri("http://localhost/"));
        var failure = await Assert.ThrowsAsync<RendezvousException>(() =>
            client.JoinAsync("123456", Guid.NewGuid(), CancellationToken.None)
        );
        Assert.Equal(expected, failure.Kind);
        Assert.Equal(code, failure.ErrorCode);
        Assert.Equal("invitations/join", failure.Operation);
        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, RendezvousFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, RendezvousFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, RendezvousFailureKind.Unreachable)]
    public async Task EmptyErrorsUseStatusClassification(HttpStatusCode status, RendezvousFailureKind expected)
    {
        using var handler = new ResponseHandler(_ => Task.FromResult(new HttpResponseMessage(status)));
        using var http = new HttpClient(handler);
        var client = new RendezvousClient(http, new Uri("http://localhost/"));
        var failure = await Assert.ThrowsAsync<RendezvousException>(() =>
            client.CloseAsync(Guid.NewGuid(), "private", CancellationToken.None)
        );
        Assert.Equal(expected, failure.Kind);
        Assert.Equal($"http_{(int)status}", failure.ErrorCode);
        Assert.DoesNotContain("private", failure.Message);
    }

    [Fact]
    public async Task TransportFailureAndHttpTimeoutHaveDistinctClassifications()
    {
        foreach (var kind in new[] { RendezvousFailureKind.Unreachable, RendezvousFailureKind.Timeout })
        {
            using var handler = new ResponseHandler(_ => Task.FromException<HttpResponseMessage>(
                    kind == RendezvousFailureKind.Timeout ? new TaskCanceledException() : new HttpRequestException()
                )
            );
            using var http = new HttpClient(handler);
            var client = new RendezvousClient(http, new Uri("http://localhost/"));
            var failure =
                await Assert.ThrowsAsync<RendezvousException>(() => client.GetDescriptorAsync(CancellationToken.None));
            Assert.Equal(kind, failure.Kind);
            Assert.Equal("transport", failure.Operation);
        }
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsTimeoutOrUnreachable()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new ResponseHandler(async token =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var client = new RendezvousClient(http, new Uri("http://localhost/"));
        var request = client.GetDescriptorAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task MalformedSuccessIsAnInvalidResponse()
    {
        using var handler = new ResponseHandler(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("not json")
                }
            )
        );
        using var http = new HttpClient(handler);
        var client = new RendezvousClient(http, new Uri("http://localhost/"));
        var failure =
            await Assert.ThrowsAsync<RendezvousException>(() => client.GetDescriptorAsync(CancellationToken.None));
        Assert.Equal(RendezvousFailureKind.InvalidResponse, failure.Kind);
    }

    [Fact]
    public async Task ErrorBodyCannotInjectArbitraryDiagnosticText()
    {
        using var handler = new ResponseHandler(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new RendezvousErrorResponse(new string('a', 5000)))
                }
            )
        );
        using var http = new HttpClient(handler);
        var client = new RendezvousClient(http, new Uri("http://localhost/"));
        var failure = await Assert.ThrowsAsync<RendezvousException>(() =>
            client.JoinAsync("123456", Guid.NewGuid(), CancellationToken.None)
        );
        Assert.Equal("http_409", failure.ErrorCode);
        Assert.Equal(RendezvousFailureKind.Rejected, failure.Kind);
    }

    private sealed class ResponseHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            respond(cancellationToken);
    }
}
