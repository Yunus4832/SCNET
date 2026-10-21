using System.Net;
using System.Net.Http.Json;

using Rendezvous.Protocol;

namespace Engine.Network.Test;

public sealed class NetworkTargetTest
{
    [Fact]
    public async Task PreparationReleasesRegistrationOnceAndRemainsClosedIfReleaseFails()
    {
        await using var host = new NetworkHost();
        var preparation = await host.PrepareAddressAsync("127.0.0.1", 1, CancellationToken.None);
        var released = 0;
        preparation.SetRegistrationRelease(_ =>
            {
                released++;
                return Task.FromException(new HttpRequestException("release unavailable"));
            }
        );
        var firstFailure = await Assert.ThrowsAsync<HttpRequestException>(() => preparation.DisposeAsync().AsTask());
        Assert.Throws<ObjectDisposedException>(() => preparation.SendDatagram("closed"u8));
        var repeatedFailure = await Assert.ThrowsAsync<HttpRequestException>(() => preparation.DisposeAsync().AsTask());
        Assert.Same(firstFailure, repeatedFailure);
        Assert.Equal(1, released);
        Assert.Null(host.Failure);
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("https://user:secret@example.com/")]
    [InlineData("https://example.com/?token=secret")]
    [InlineData("https://example.com/#fragment")]
    public void InvitationRejectsInsecureOrCredentialBearingControlAddress(string address)
    {
        Assert.Throws<ArgumentException>(() => new InvitationCodeNetworkTarget(new Uri(address), "012345"));
    }

    [Fact]
    public async Task DirectPreparationKeepsDnsTargetAndUsesSameSocketForProbeAndHandshake()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        using var http = new HttpClient();
        server.ConnectionRequested += request => request.Accept();
        server.DatagramReceived += (source, payload) => source.Send(payload);
        var target = new AddressNetworkTarget("localhost", server.LocalPort);
        await using var preparation = await client.PrepareConnectionAsync(target, http, CancellationToken.None);
        Assert.Equal(target, preparation.Target);
        byte[]? received = null;
        preparation.DatagramReceived += payload => received = payload;
        preparation.SendDatagram("before-handshake"u8);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (received is null)
        {
            timeout.Token.ThrowIfCancellationRequested();
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(2, timeout.Token);
        }

        Assert.Equal("before-handshake"u8.ToArray(), received);
        var connecting = preparation.ConnectAsync(ReadOnlyMemory<byte>.Empty, timeout.Token);
        while (!connecting.IsCompleted)
        {
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(2, timeout.Token);
        }

        Assert.True((await connecting).IsConnected);
        Assert.Equal(NetworkPathKind.Direct, preparation.PathKind);
    }

    [Fact]
    public async Task InvalidBeaconDescriptorCannotStartAnInvitationJoin()
    {
        await using var host = new NetworkHost();
        using var handler = new DescriptorHandler();
        using var http = new HttpClient(handler);
        var target = new InvitationCodeNetworkTarget(new Uri("http://localhost/"), "012345");
        var failure =
            await Assert.ThrowsAsync<RendezvousException>(() =>
                host.PrepareConnectionAsync(target, http, CancellationToken.None)
            );
        Assert.Equal(RendezvousFailureKind.InvalidResponse, failure.Kind);
        Assert.Equal(1, handler.Requests);
    }

    private sealed class DescriptorHandler : HttpMessageHandler
    {
        internal int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/transport", request.RequestUri!.AbsolutePath);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new RendezvousDescriptor(0, null))
                }
            );
        }
    }
}
