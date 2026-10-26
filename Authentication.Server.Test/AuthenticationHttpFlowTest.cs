using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;
using SCNET.Authentication.Server.Infrastructure;

namespace Authentication.Server.Test;

public sealed class AuthenticationHttpFlowTest
{
    [Fact]
    public async Task BrowserApprovalSignedPollingCachedAdmissionAndRevocationWorkOverHttp()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory = CreateFactory(directory);
            using var browser = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://auth.test/"), AllowAutoRedirect = false
                }
            );
            var runtime = factory.Services.GetRequiredService<ProviderRuntime>();
            using var protocol = new AuthenticationHttpClient(runtime.Service, factory.Server.CreateHandler());
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var transaction = await protocol.CreateTransactionAsync(
                user,
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login)
            );
            Assert.Equal(AuthenticationTransactionState.Pending, transaction.State);
            var browserUri = new Uri(transaction.BrowserUrl!);
            var browserSecret = browserUri.Fragment[1..];
            var page = await browser.GetStringAsync(browserUri.AbsolutePath);
            Assert.Contains(AuthenticationPublicKey.From(user).Address, page);
            Assert.DoesNotContain(browserSecret, page);
            using var approved = await browser.PostAsync(
                browserUri.AbsolutePath,
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["account"] = "test-account", ["password"] = "provider-test-password",
                        ["browserSecret"] = browserSecret
                    }
                )
            );
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            var completed = await protocol.PollAsync(user, transaction.TransactionId);
            Assert.Equal(AuthenticationTransactionState.Completed, completed.State);
            Assert.NotNull(completed.Grant);
            Assert.True(File.Exists(Path.Combine(directory, "authorizations.json")));
            Assert.DoesNotContain(
                completed.Grant,
                await File.ReadAllTextAsync(Path.Combine(directory, "authorizations.json"))
            );
            var challenge = new AdmissionChallenge(
                AuthenticationPublicKey.From(user).Address,
                AuthenticationPublicKey.From(server).Address,
                AuthenticationNonce.Create(),
                AuthenticationNonce.Create(),
                DateTimeOffset.UtcNow.AddSeconds(90).ToUnixTimeSeconds()
            );
            var admitted = await protocol.CreateTransactionAsync(
                user,
                new AuthenticationTransactionRequest(AuthenticationPurpose.Join, challenge, completed.Grant)
            );
            Assert.Null(admitted.BrowserUrl);
            Assert.Null(admitted.Grant);
            Assert.NotNull(admitted.Approval);
            var approval = await AdmissionApprovalTokens.ValidateAsync(
                admitted.Approval,
                runtime.PublicKey,
                challenge,
                DateTimeOffset.UtcNow
            );
            Assert.Equal(challenge.Subject, approval.Subject);
            await protocol.RevokeGrantAsync(user, completed.Grant);
            var revoked = await Assert.ThrowsAsync<AuthenticationRemoteException>(() => protocol.CreateTransactionAsync(
                    user,
                    new AuthenticationTransactionRequest(AuthenticationPurpose.Join, challenge, completed.Grant)
                )
            );
            Assert.Equal(AuthenticationError.GrantRevoked, revoked.Error);
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public async Task HttpProofReplayAndCancelBeforeBrowserCompletionAreRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory = CreateFactory(directory);
            using var browser = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://auth.test/"), AllowAutoRedirect = false
                }
            );
            var runtime = factory.Services.GetRequiredService<ProviderRuntime>();
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var proof = AuthenticationRequestProofs.Create(
                user,
                runtime.PublicKey.Address,
                runtime.Service.GetUri(AuthenticationProtocol.TransactionsPath),
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login),
                DateTimeOffset.UtcNow
            );
            using var initial = await browser.PostAsJsonAsync("/v1/transactions", proof);
            Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
            using var replay = await browser.PostAsJsonAsync("/v1/transactions", proof);
            Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
            using var protocol = new AuthenticationHttpClient(runtime.Service, factory.Server.CreateHandler());
            var transaction = await protocol.CreateTransactionAsync(
                user,
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login)
            );
            var cancelled = await protocol.CancelAsync(user, transaction.TransactionId);
            Assert.Equal(AuthenticationTransactionState.Cancelled, cancelled.State);
            var uri = new Uri(transaction.BrowserUrl!);
            using var late = await browser.PostAsync(
                uri.AbsolutePath,
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["account"] = "test-account", ["password"] = "provider-test-password",
                        ["browserSecret"] = uri.Fragment[1..]
                    }
                )
            );
            Assert.Equal(HttpStatusCode.Forbidden, late.StatusCode);
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string directory)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.Configure<ProviderOptions>(options =>
                    {
                        options.PublicUrl = "https://auth.test/";
                        options.DataDirectory = directory;
                        options.Accounts = new Dictionary<string, string>
                        {
                            ["test-account"] = new PasswordHasher<object>().HashPassword(
                                new object(),
                                "provider-test-password"
                            )
                        };
                    }
                )
            )
        );
    }
}
