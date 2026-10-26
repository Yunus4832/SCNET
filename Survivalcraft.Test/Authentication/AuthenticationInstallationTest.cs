using System.Security.Cryptography;

using Game.Authentication;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Authentication;

public sealed class AuthenticationInstallationTest
{
    [Fact]
    public void DirectoryMirrorsNeverReenableServicesOrReplaceExistingTrustAndGrants()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-install-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = CreateManager(directory);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider, "https://authority.example/");
            var subject = AuthenticationPublicKey.From(user).Address;
            Assert.Equal(AuthenticationServiceInstallationStatus.Available, manager.GetInstallationStatus(endpoint));
            manager.InstallConfirmedService(endpoint, "https://first-directory.example/");
            Assert.Equal(AuthenticationServiceInstallationStatus.Installed, manager.GetInstallationStatus(endpoint));
            manager.Services.SetEnabled(endpoint.Identity.Address, false);
            manager.Grants.Set(
                subject,
                endpoint,
                AuthenticationNonce.Create(),
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
            );
            Assert.Equal(AuthenticationServiceInstallationStatus.Disabled, manager.GetInstallationStatus(endpoint));
            manager.InstallConfirmedService(endpoint, "https://mirror-directory.example/");
            var saved = CreateManager(directory);
            var installed = Assert.Single(saved.Services.Services);
            Assert.False(installed.Enabled);
            Assert.Equal("https://first-directory.example/", installed.Source);
            Assert.NotNull(saved.Grants.Find(subject, endpoint));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void ConfirmedAddressChangeClearsOldGrantsButRetainsDisabledStateAndIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = CreateManager(directory);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider, "https://old.example/");
            var moved = Endpoint(provider, "https://new.example/");
            var subject = AuthenticationPublicKey.From(user).Address;
            manager.InstallConfirmedService(endpoint, "https://directory.example/");
            manager.Services.SetEnabled(endpoint.Identity.Address, false);
            manager.Grants.Set(
                subject,
                endpoint,
                AuthenticationNonce.Create(),
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
            );
            Assert.Equal(AuthenticationServiceInstallationStatus.EndpointChange, manager.GetInstallationStatus(moved));
            manager.InstallConfirmedService(moved, "https://mirror.example/");
            var saved = CreateManager(directory);
            var installed = Assert.Single(saved.Services.Services);
            Assert.Equal(moved.BaseUri, installed.Endpoint.BaseUri);
            Assert.Equal(endpoint.Identity.Address, installed.Endpoint.Identity.Address);
            Assert.False(installed.Enabled);
            Assert.Null(saved.Grants.Find(subject, endpoint));
            Assert.Null(saved.Grants.Find(subject, moved));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void SameAddressWithAnotherKeyCannotOverwriteOrClearInstalledIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-conflict-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = CreateManager(directory);
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider, "https://authority.example/");
            var changed = Endpoint(other, "https://authority.example/");
            var subject = AuthenticationPublicKey.From(user).Address;
            manager.InstallConfirmedService(endpoint, "https://directory.example/");
            manager.Grants.Set(
                subject,
                endpoint,
                AuthenticationNonce.Create(),
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
            );
            Assert.Equal(AuthenticationServiceInstallationStatus.KeyConflict, manager.GetInstallationStatus(changed));
            Assert.Throws<InvalidOperationException>(() =>
                manager.InstallConfirmedService(changed, "https://mirror.example/")
            );
            var saved = CreateManager(directory);
            Assert.Equal(endpoint.Identity.Address, Assert.Single(saved.Services.Services).Endpoint.Identity.Address);
            Assert.NotNull(saved.Grants.Find(subject, endpoint));
            Assert.Null(saved.Services.Find(changed.Identity.Address));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    private static Game.Managers.AuthenticationManager CreateManager(string directory)
    {
        return new Game.Managers.AuthenticationManager(
            new AuthenticationServiceRegistry(Path.Combine(directory, "services.xml")),
            new AuthenticationGrantStore(Path.Combine(directory, "grants.xml")),
            new ServerCredentialStore(Path.Combine(directory, "credentials.xml"))
        );
    }

    private static AuthenticationServiceEndpoint Endpoint(ECDsa key, string url)
    {
        return new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Authority",
                url,
                AuthenticationPublicKey.From(key).EncodedKey
            )
        );
    }
}
