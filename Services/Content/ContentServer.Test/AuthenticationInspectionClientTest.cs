using System.Net;
using System.Net.Http.Json;
using System.Text;

using ContentServer.Application;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Test;

public sealed class AuthenticationInspectionClientTest
{
    [Fact]
    public async Task InspectionIsAnonymousAndChecksTheSourceWithoutPinningIdentity()
    {
        var expected = AuthenticationServiceRegistrationTest.Endpoint();
        var calls = 0;
        var inspection = new AuthenticationInspectionClient(() => new Handler(request =>
                {
                    calls++;
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal(expected.GetUri(AuthenticationProtocol.DiscoveryPath), request.RequestUri);
                    Assert.Null(request.Content);
                    Assert.Null(request.Headers.Authorization);
                    Assert.False(request.Headers.Contains("Cookie"));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                        { Content = JsonContent.Create(expected.Description) };
                }
            )
        );
        await inspection.InspectAsync(expected.BaseUri, CancellationToken.None);
        Assert.Equal(1, calls);

        var other = AuthenticationServiceRegistrationTest.Endpoint();
        var conflict = new AuthenticationInspectionClient(() => new Handler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(other.Description)
                }
            )
        );
        await conflict.InspectAsync(expected.BaseUri, CancellationToken.None);

        var relocated = expected.Description with { ServiceUrl = "https://other.example/" };
        var moved = new AuthenticationInspectionClient(() => new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(relocated)
                }
            )
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() =>
            moved.InspectAsync(expected.BaseUri, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RedirectAndUnboundedBodyAreRejectedWithoutFollowingAnotherEndpoint()
    {
        var expected = AuthenticationServiceRegistrationTest.Endpoint();
        var calls = 0;
        var redirect = new AuthenticationInspectionClient(() => new Handler(_ =>
                {
                    calls++;
                    var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                    response.Headers.Location = new Uri("http://127.0.0.1/private");
                    return response;
                }
            )
        );
        await Assert.ThrowsAsync<AuthenticationRemoteException>(() => redirect.InspectAsync(
                expected.BaseUri,
                CancellationToken.None
            )
        );
        Assert.Equal(1, calls);

        var oversized = new AuthenticationInspectionClient(() => new Handler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(new string('x', 32769))))
                }
            )
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() =>
            oversized.InspectAsync(expected.BaseUri, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CancellationStopsInspectionWithoutSendingAnotherRequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var inspection = new AuthenticationInspectionClient(() =>
            new Handler(_ => throw new InvalidOperationException("Cancelled request reached transport."))
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            inspection.InspectAsync(AuthenticationServiceRegistrationTest.Endpoint().BaseUri, cancellation.Token)
        );
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("198.18.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001::1")]
    [InlineData("2002:a00:1::1")]
    [InlineData("3fff::1")]
    public void DnsCannotSelectPrivateSpecialOrMixedAddresses(string value)
    {
        var address = IPAddress.Parse(value);
        Assert.Throws<HttpRequestException>(() => AuthenticationInspectionClient.RequirePublicAddress([address]));
        Assert.Throws<HttpRequestException>(() =>
            AuthenticationInspectionClient.RequirePublicAddress([IPAddress.Parse("8.8.8.8"), address])
        );
    }

    [Fact]
    public void PublicAddressSelectionUsesTheAlreadyValidatedAddress()
    {
        var address = IPAddress.Parse("2606:4700:4700::1111");
        Assert.Same(
            address,
            AuthenticationInspectionClient.RequirePublicAddress([address, IPAddress.Parse("8.8.8.8")])
        );
        Assert.Throws<HttpRequestException>(() => AuthenticationInspectionClient.RequirePublicAddress([]));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
