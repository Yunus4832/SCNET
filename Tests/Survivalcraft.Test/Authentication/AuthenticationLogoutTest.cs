using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Game.Authentication;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Authentication;

public sealed class AuthenticationLogoutTest
{
    [Theory]
    [InlineData("success", true)]
    [InlineData("unavailable", false)]
    [InlineData("key_changed", false)]
    public async Task LogoutClearsLocalGrantBeforeRemoteWorkAndNeverSendsItToChangedKeys(string state, bool expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-logout-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider);
            var subject = AuthenticationPublicKey.From(key).Address;
            var manager = new Game.Managers.AuthenticationManager(
                new AuthenticationServiceRegistry(Path.Combine(directory, "services.xml")),
                new AuthenticationGrantStore(Path.Combine(directory, "grants.xml")),
                new ServerCredentialStore(Path.Combine(directory, "credentials.xml"))
            );
            manager.Services.AddConfirmed(endpoint);
            var secret = AuthenticationNonce.Create();
            manager.Grants.Set(subject, endpoint, secret, DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
            var posted = 0;
            var success = await manager.LogoutAsync(
                key,
                manager.Services.Find(endpoint.Identity.Address)!,
                CancellationToken.None,
                service => new AuthenticationHttpClient(
                    service,
                    new Handler(async request =>
                        {
                            Assert.Null(manager.Grants.Find(subject, endpoint));
                            if (request.Method == HttpMethod.Get)
                            {
                                return state switch
                                {
                                    "unavailable" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                                    "key_changed" => Reply(Endpoint(other).Description),
                                    _ => Reply(endpoint.Description)
                                };
                            }

                            posted++;
                            var proof = (await request.Content!.ReadFromJsonAsync<SignedAuthenticationRequest>())!;
                            var verified =
                                await AuthenticationRequestProofs.ValidateAsync<AuthenticationGrantRevocation>(
                                    proof,
                                    endpoint.Identity.Address,
                                    request.RequestUri!,
                                    DateTimeOffset.UtcNow
                                );
                            Assert.Equal(subject, verified.Subject);
                            Assert.Equal(secret, verified.Data.Grant);
                            return Reply(new AuthenticationErrorResponse(AuthenticationError.None));
                        }
                    )
                )
            );
            Assert.Equal(expected, success);
            Assert.Equal(expected ? 1 : 0, posted);
            Assert.Null(manager.Grants.Find(subject, endpoint));
            Assert.NotNull(manager.Services.Find(endpoint.Identity.Address));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void RemovingProviderClearsItsGrantsWithoutChangingOtherProviders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-remove-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var one = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var two = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var first = Endpoint(one);
            var second = Endpoint(two);
            var manager = new Game.Managers.AuthenticationManager(
                new AuthenticationServiceRegistry(Path.Combine(directory, "services.xml")),
                new AuthenticationGrantStore(Path.Combine(directory, "grants.xml")),
                new ServerCredentialStore(Path.Combine(directory, "credentials.xml"))
            );
            manager.Services.AddConfirmed(first);
            manager.Services.AddConfirmed(second);
            var subject = first.Identity.Address;
            foreach (var endpoint in new[] { first, second })
            {
                manager.Grants.Set(
                    subject,
                    endpoint,
                    AuthenticationNonce.Create(),
                    DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
                );
            }

            manager.RemoveService(first.Identity.Address);
            Assert.Null(manager.Services.Find(first.Identity.Address));
            Assert.Null(manager.Grants.Find(subject, first));
            Assert.NotNull(manager.Services.Find(second.Identity.Address));
            Assert.NotNull(manager.Grants.Find(subject, second));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    private static AuthenticationServiceEndpoint Endpoint(ECDsa provider)
    {
        return new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Test",
                "https://auth.test/",
                AuthenticationPublicKey.From(provider).EncodedKey
            )
        );
    }

    private static HttpResponseMessage Reply<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                body,
                options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
                }
            )
        };
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return respond(request);
        }
    }
}
