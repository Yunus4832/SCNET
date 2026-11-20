using System.Security.Cryptography;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Security;
using SCNET.Authentication.Server.Infrastructure;

namespace Authentication.Server.Test;

public sealed class ProviderPersistenceTest
{
    [Fact]
    public void RestartRetainsProviderIdentityGrantsAndAccountBansAndPreventsConcurrentWriters()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new ProviderOptions { PublicUrl = "https://auth.example/", DataDirectory = directory };
            using var player = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var subject = AuthenticationPublicKey.From(player).Address;
            var now = DateTimeOffset.UtcNow;
            string providerAddress;
            string grant;
            using (var first = new ProviderRuntime(options))
            {
                providerAddress = first.PublicKey.Address;
                Assert.Throws<IOException>(() => new ProviderRuntime(options));
                var transaction = first.Execute(authority => authority.Begin(
                        subject,
                        new AuthenticationTransactionRequest(AuthenticationPurpose.Login),
                        now
                    )
                );
                Assert.True(
                    first.Execute(
                        authority => authority.ApproveBrowser(
                            transaction.TransactionId,
                            new Uri(transaction.BrowserUrl!).Fragment[1..],
                            "account",
                            now
                        ),
                        true
                    )
                );
                grant = first.Execute(authority => authority.Poll(subject, transaction.TransactionId, now)).Grant!;
                if (!OperatingSystem.IsWindows())
                {
                    Assert.Equal(
                        UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        File.GetUnixFileMode(Path.Combine(directory, "identity.p8"))
                    );
                }
            }

            using (var second = new ProviderRuntime(options))
            {
                Assert.Equal(providerAddress, second.PublicKey.Address);
                Assert.Throws<InvalidOperationException>(() => second.Execute<bool>(
                        authority =>
                        {
                            authority.SetAccountBan("account", true);
                            throw new InvalidOperationException("Simulated failed provider command.");
                        },
                        true
                    )
                );
                Assert.Equal(
                    AuthenticationTransactionState.Completed,
                    second.Execute(authority => authority.Begin(
                            subject,
                            new AuthenticationTransactionRequest(AuthenticationPurpose.Login, Grant: grant),
                            now.AddSeconds(3)
                        )
                    ).State
                );
                second.Execute(
                    authority =>
                    {
                        authority.SetAccountBan("account", true);
                        return true;
                    },
                    true
                );
            }

            using (var third = new ProviderRuntime(options))
            {
                var failure = Assert.Throws<SCNET.Authentication.Protocol.Http.AuthenticationRemoteException>(() =>
                    third.Execute(authority => authority.Begin(
                            subject,
                            new AuthenticationTransactionRequest(AuthenticationPurpose.Login, Grant: grant),
                            now.AddSeconds(4)
                        )
                    )
                );
                Assert.Equal(AuthenticationError.SubjectBanned, failure.Error);
            }

            File.Delete(Path.Combine(directory, "identity.p8"));
            Assert.Throws<InvalidDataException>(() => new ProviderRuntime(options));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }
}
