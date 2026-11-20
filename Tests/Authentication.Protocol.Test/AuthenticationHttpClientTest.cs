using System.Net;
using System.Security.Cryptography;
using System.Text;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Authentication.Protocol.Test;

public sealed class AuthenticationHttpClientTest
{
    [Theory]
    [InlineData("http://auth.example/")]
    [InlineData("https://user:password@auth.example/")]
    [InlineData("https://auth.example/?redirect=other")]
    [InlineData("https://auth.example/#other")]
    [InlineData("https://auth.example/no-trailing-slash")]
    public void InvalidServiceLocationsAreRejectedBeforeAnyNetworkRequest(string url)
    {
        Assert.Throws<AuthenticationProtocolException>(() => AuthenticationServiceEndpoint.ParseBaseUri(url));
    }

    [Fact]
    public async Task RedirectResponseDoesNotReceiveASecondRequestContainingTheGrant()
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var count = 0;
        using var client = new AuthenticationHttpClient(
            Endpoint(authority),
            new ResponseHandler(request =>
                {
                    count++;
                    Assert.Equal("auth.example", request.RequestUri!.Host);
                    return new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
                    {
                        Headers = { Location = new Uri("https://attacker.invalid/collect") }
                    };
                }
            )
        );
        var exception = await Assert.ThrowsAsync<AuthenticationRemoteException>(() => client.CreateTransactionAsync(
                user,
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login, Grant: AuthenticationNonce.Create())
            )
        );
        Assert.Equal(AuthenticationError.InvalidRequest, exception.Error);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ChunkedOversizeResponseIsRejectedWithoutTrustingContentLength()
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var client = new AuthenticationHttpClient(
            Endpoint(authority),
            new ResponseHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(new string(' ', 32769))))
                }
            )
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => client.CreateTransactionAsync(
                user,
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login)
            )
        );
    }

    private static AuthenticationServiceEndpoint Endpoint(ECDsa key)
    {
        return new(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Test",
                "https://auth.example/",
                AuthenticationPublicKey.From(key).EncodedKey
            )
        );
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(respond(request));
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
