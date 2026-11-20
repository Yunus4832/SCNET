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

public sealed class AuthenticationLoginClientTest
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    [Theory]
    [InlineData(AuthenticationError.None, false)]
    [InlineData(AuthenticationError.GrantExpired, false)]
    [InlineData(AuthenticationError.GrantRevoked, false)]
    [InlineData(AuthenticationError.GrantRevoked, true)]
    [InlineData(AuthenticationError.SubjectBanned, false)]
    [InlineData(AuthenticationError.Unavailable, false)]
    public async Task CachedLoginOnlyFallsBackForExpiredOrRevokedGrants(AuthenticationError failure, bool interruptPoll)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-login-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider);
            var subject = AuthenticationPublicKey.From(user).Address;
            var grants = new AuthenticationGrantStore(Path.Combine(directory, "grants.xml"));
            var oldSecret = AuthenticationNonce.Create();
            var newSecret = AuthenticationNonce.Create();
            var grantExpiry = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
            grants.Set(subject, endpoint, oldSecret, grantExpiry);
            var challenge = new AdmissionChallenge(
                subject,
                AuthenticationPublicKey.From(server).Address,
                AuthenticationNonce.Create(),
                AuthenticationNonce.Create(),
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
            );
            var browserCalls = 0;
            var creates = 0;
            var polls = 0;
            var client = new AuthenticationLoginClient(
                grants,
                service => new AuthenticationHttpClient(
                    service,
                    new Handler(async request =>
                        {
                            if (request.Method == HttpMethod.Get) { return Reply(service.Description); }

                            var proof = (await request.Content!.ReadFromJsonAsync<SignedAuthenticationRequest>())!;
                            if (request.RequestUri!.AbsolutePath.EndsWith("/poll", StringComparison.Ordinal))
                            {
                                var verified =
                                    await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionCommand>(
                                        proof,
                                        endpoint.Identity.Address,
                                        request.RequestUri,
                                        DateTimeOffset.UtcNow
                                    );
                                Assert.Equal(subject, verified.Subject);
                                polls++;
                                if (interruptPoll && polls == 1)
                                {
                                    throw new HttpRequestException("Background network unavailable.");
                                }

                                return Reply(
                                    new AuthenticationTransactionResult(
                                        "transaction",
                                        AuthenticationTransactionState.Completed,
                                        challenge.ExpiresAt,
                                        1,
                                        Grant: newSecret,
                                        GrantExpiresAt: grantExpiry,
                                        Approval: AdmissionApprovalTokens.Issue(
                                            provider,
                                            challenge,
                                            DateTimeOffset.UtcNow
                                        )
                                    )
                                );
                            }

                            var creation =
                                await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
                                    proof,
                                    endpoint.Identity.Address,
                                    request.RequestUri,
                                    DateTimeOffset.UtcNow
                                );
                            creates++;
                            Assert.Equal(subject, creation.Subject);
                            Assert.Equal(challenge, creation.Data.Challenge);
                            if (creation.Data.Grant is not null)
                            {
                                Assert.Equal(oldSecret, creation.Data.Grant);
                                return failure == AuthenticationError.None
                                    ? Reply(
                                        new AuthenticationTransactionResult(
                                            "transaction",
                                            AuthenticationTransactionState.Completed,
                                            challenge.ExpiresAt,
                                            1,
                                            Approval: AdmissionApprovalTokens.Issue(
                                                provider,
                                                challenge,
                                                DateTimeOffset.UtcNow
                                            )
                                        )
                                    )
                                    : Reply(new AuthenticationErrorResponse(failure), HttpStatusCode.BadRequest);
                            }

                            return Reply(
                                new AuthenticationTransactionResult(
                                    "transaction",
                                    AuthenticationTransactionState.Pending,
                                    challenge.ExpiresAt,
                                    1,
                                    BrowserUrl: "https://auth.test/login#secret"
                                )
                            );
                        }
                    )
                )
            );

            Task OpenBrowser(Uri uri, CancellationToken token)
            {
                browserCalls++;
                Assert.Equal("auth.test", uri.Host);
                return Task.CompletedTask;
            }

            if (failure is AuthenticationError.SubjectBanned or AuthenticationError.Unavailable)
            {
                var error = await Assert.ThrowsAsync<AuthenticationRemoteException>(() => client.AuthenticateAsync(
                        user,
                        endpoint,
                        challenge,
                        true,
                        OpenBrowser,
                        CancellationToken.None
                    )
                );
                Assert.Equal(failure, error.Error);
                Assert.Equal(0, browserCalls);
                Assert.Equal(1, creates);
            }
            else
            {
                var result = await client.AuthenticateAsync(
                    user,
                    endpoint,
                    challenge,
                    true,
                    OpenBrowser,
                    CancellationToken.None
                );
                Assert.NotNull(result.Approval);
                Assert.Equal(failure == AuthenticationError.None ? oldSecret : newSecret, result.Grant!.Secret);
                Assert.Equal(failure == AuthenticationError.None ? 0 : 1, browserCalls);
                Assert.Equal(failure == AuthenticationError.None ? 0 : interruptPoll ? 2 : 1, polls);
                Assert.Equal(failure == AuthenticationError.None ? 1 : 2, creates);
                if (failure != AuthenticationError.None) { Assert.Null(grants.Find(subject, endpoint)); }
            }
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrUntrustedBrowserEntryCannotPublishCredentials(bool foreignBrowser)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider);
            var grants = new AuthenticationGrantStore(Path.Combine(directory, "grants.xml"));
            var cancelled = false;
            var browserCalls = 0;
            using var cancellation = new CancellationTokenSource();
            var client = new AuthenticationLoginClient(
                grants,
                service => new AuthenticationHttpClient(
                    service,
                    new Handler(async request =>
                        {
                            if (request.Method == HttpMethod.Get) { return Reply(service.Description); }

                            var proof = (await request.Content!.ReadFromJsonAsync<SignedAuthenticationRequest>())!;
                            if (request.RequestUri!.AbsolutePath.EndsWith("/cancel", StringComparison.Ordinal))
                            {
                                await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionCommand>(
                                    proof,
                                    endpoint.Identity.Address,
                                    request.RequestUri,
                                    DateTimeOffset.UtcNow
                                );
                                cancelled = true;
                                return Reply(
                                    new AuthenticationTransactionResult(
                                        "transaction",
                                        AuthenticationTransactionState.Cancelled,
                                        DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
                                        1,
                                        Error: AuthenticationError.Cancelled
                                    )
                                );
                            }

                            return Reply(
                                new AuthenticationTransactionResult(
                                    "transaction",
                                    AuthenticationTransactionState.Pending,
                                    DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
                                    1,
                                    BrowserUrl: foreignBrowser
                                        ? "https://untrusted.test/login#secret"
                                        : "https://auth.test/login#secret"
                                )
                            );
                        }
                    )
                )
            );

            Task OpenBrowser(Uri uri, CancellationToken token)
            {
                browserCalls++;
                cancellation.Cancel();
                return Task.CompletedTask;
            }

            if (foreignBrowser)
            {
                await Assert.ThrowsAsync<AuthenticationProtocolException>(() => client.AuthenticateAsync(
                        user,
                        endpoint,
                        null,
                        false,
                        OpenBrowser,
                        cancellation.Token
                    )
                );
                Assert.Equal(0, browserCalls);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.AuthenticateAsync(
                        user,
                        endpoint,
                        null,
                        false,
                        OpenBrowser,
                        cancellation.Token
                    )
                );
                Assert.Equal(1, browserCalls);
            }

            Assert.True(cancelled);
            Assert.Null(grants.Find(AuthenticationPublicKey.From(user).Address, endpoint));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public async Task DiscoveryKeyConflictStopsBeforePostingAnyCachedCredential()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-pin-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var changed = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider);
            var subject = AuthenticationPublicKey.From(user).Address;
            var store = new AuthenticationGrantStore(Path.Combine(directory, "grants.xml"));
            store.Set(
                subject,
                endpoint,
                AuthenticationNonce.Create(),
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
            );
            var client = new AuthenticationLoginClient(
                store,
                service => new AuthenticationHttpClient(
                    service,
                    new Handler(request =>
                        {
                            Assert.Equal(HttpMethod.Get, request.Method);
                            return Task.FromResult(Reply(Endpoint(changed).Description));
                        }
                    )
                )
            );
            await Assert.ThrowsAsync<AuthenticationServiceIdentityException>(() => client.AuthenticateAsync(
                    user,
                    endpoint,
                    null,
                    true,
                    (_, _) => throw new InvalidOperationException("Browser must not open"),
                    CancellationToken.None
                )
            );
            Assert.NotNull(store.Find(subject, endpoint));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Theory]
    [InlineData(AuthenticationError.None, false)]
    [InlineData(AuthenticationError.GrantExpired, false)]
    [InlineData(AuthenticationError.GrantRevoked, false)]
    [InlineData(AuthenticationError.None, true)]
    public async Task TemporaryGrantLoginAndExpiryNeverMutatePersistentAuthorization(
        AuthenticationError failure,
        bool expired
    )
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-transient-login-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider);
            var subject = AuthenticationPublicKey.From(user).Address;
            var store = new AuthenticationGrantStore(Path.Combine(directory, "grants.xml"));
            var persistent = AuthenticationNonce.Create();
            var temporary = AuthenticationNonce.Create();
            var replacement = AuthenticationNonce.Create();
            var expiry = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
            store.Set(subject, endpoint, persistent, expiry);
            var grant = new StoredAuthenticationGrant(
                subject,
                endpoint.Identity.Address,
                endpoint.BaseUri.AbsoluteUri,
                temporary,
                expired ? DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds() : expiry
            );
            var creates = 0;
            var client = new AuthenticationLoginClient(
                store,
                service => new AuthenticationHttpClient(
                    service,
                    new Handler(async request =>
                        {
                            if (request.Method == HttpMethod.Get) { return Reply(service.Description); }

                            var proof = (await request.Content!.ReadFromJsonAsync<SignedAuthenticationRequest>())!;
                            var creation =
                                await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
                                    proof,
                                    endpoint.Identity.Address,
                                    request.RequestUri!,
                                    DateTimeOffset.UtcNow
                                );
                            creates++;
                            if (creates == 1 && !expired)
                            {
                                Assert.True(creation.Data.Grant == temporary);
                                if (failure != AuthenticationError.None)
                                {
                                    return Reply(new AuthenticationErrorResponse(failure), HttpStatusCode.Unauthorized);
                                }
                            }
                            else { Assert.Null(creation.Data.Grant); }

                            return Reply(
                                new AuthenticationTransactionResult(
                                    "temporary-transaction",
                                    AuthenticationTransactionState.Completed,
                                    DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
                                    1,
                                    Grant: replacement,
                                    GrantExpiresAt: expiry
                                )
                            );
                        }
                    )
                )
            );
            var result = await client.AuthenticateAsync(
                user,
                endpoint,
                null,
                false,
                (_, _) => throw new InvalidOperationException("Browser must not open for this fixture."),
                CancellationToken.None,
                grant
            );
            Assert.True(result.Grant!.Secret == replacement);
            Assert.True(store.Find(subject, endpoint)!.Secret == persistent);
            var reloaded = new AuthenticationGrantStore(Path.Combine(directory, "grants.xml"));
            Assert.True(reloaded.Find(subject, endpoint)!.Secret == persistent);
            Assert.Equal(failure == AuthenticationError.None || expired ? 1 : 2, creates);
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("authority")]
    [InlineData("url")]
    public async Task UnboundTemporaryGrantIsRejectedBeforeAnyHttpRequest(string mismatch)
    {
        using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var endpoint = Endpoint(provider);
        var grant = new StoredAuthenticationGrant(
            AuthenticationPublicKey.From(user).Address,
            endpoint.Identity.Address,
            endpoint.BaseUri.AbsoluteUri,
            AuthenticationNonce.Create(),
            DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
        );
        grant = mismatch switch
        {
            "subject" => grant with { Subject = endpoint.Identity.Address },
            "authority" => grant with { Authority = grant.Subject },
            _ => grant with { ServiceUrl = "https://other.example/" }
        };
        var store = new AuthenticationGrantStore(
            Path.Combine(Path.GetTempPath(), "unused-auth-store-" + Guid.NewGuid().ToString("N"))
        );
        var requests = 0;
        var client = new AuthenticationLoginClient(
            store,
            service => new AuthenticationHttpClient(
                service,
                new Handler(_ =>
                    {
                        requests++;
                        throw new InvalidOperationException("Mismatched grant reached the network.");
                    }
                )
            )
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => client.AuthenticateAsync(
                user,
                endpoint,
                null,
                false,
                (_, _) => Task.CompletedTask,
                CancellationToken.None,
                grant
            )
        );
        Assert.Equal(0, requests);
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

    private static HttpResponseMessage Reply<T>(T body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = JsonContent.Create(body, options: _json) };
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
