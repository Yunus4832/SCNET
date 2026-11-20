using System.Security.Cryptography;
using System.Text.Json;

using Game.Authentication;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Authentication;

public sealed class AuthenticationStorageTest
{
    [Fact]
    public void RegistryPersistsOnlyExplicitTrustChangesAndDoesNotReenableDisabledProviders()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-registry-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(key);
            var path = Path.Combine(directory, "services.xml");
            var registry = new AuthenticationServiceRegistry(path);
            Assert.False(File.Exists(path));
            registry.AddConfirmed(endpoint);
            registry.SetEnabled(endpoint.Identity.Address, false);
            registry = new AuthenticationServiceRegistry(path);
            Assert.False(registry.Find(endpoint.Identity.Address)!.Enabled);
            Assert.Throws<InvalidOperationException>(() => registry.AddConfirmed(endpoint));
            Assert.Throws<InvalidOperationException>(() =>
                registry.UpdateConfirmedEndpoint(endpoint.Identity.Address, Endpoint(other))
            );
            registry.UpdateConfirmedEndpoint(endpoint.Identity.Address, Endpoint(key, "https://moved.example/"));
            registry = new AuthenticationServiceRegistry(path);
            Assert.False(registry.Find(endpoint.Identity.Address)!.Enabled);
            Assert.Equal("moved.example", registry.Find(endpoint.Identity.Address)!.Endpoint.BaseUri.Host);
            registry.SetEnabled(endpoint.Identity.Address, true);
            registry.Remove(endpoint.Identity.Address);
            Assert.Empty(new AuthenticationServiceRegistry(path).Services);
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void GrantsAreSeparatedByUserAuthorityAndConfirmedEndpointAndRemainPrivateOnDisk()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-grants-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var user = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var otherUser = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(provider);
            var subject = AuthenticationPublicKey.From(user).Address;
            var path = Path.Combine(directory, "grants.xml");
            var grants = new AuthenticationGrantStore(path);
            var secret = AuthenticationNonce.Create();
            grants.Set(subject, endpoint, secret, DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
            grants = new AuthenticationGrantStore(path);
            var found = Assert.IsType<StoredAuthenticationGrant>(grants.Find(subject, endpoint));
            Assert.Equal(secret, found.Secret);
            Assert.True(found.IsValid(DateTimeOffset.UtcNow));
            Assert.DoesNotContain(secret, found.ToString());
            Assert.DoesNotContain(secret, JsonSerializer.Serialize(found));
            Assert.Null(grants.Find(AuthenticationPublicKey.From(otherUser).Address, endpoint));
            Assert.Null(grants.Find(subject, Endpoint(provider, "https://unknown.example/")));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }

            grants.Remove(subject, endpoint.Identity.Address);
            Assert.Null(new AuthenticationGrantStore(path).Find(subject, endpoint));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    [Fact]
    public void FailedPersistenceDoesNotPublishAnInstalledServiceOrCachedGrant()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scnet-auth-storage-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var endpoint = Endpoint(key);
            var registryPath = Path.Combine(directory, "services.xml");
            var grantsPath = Path.Combine(directory, "grants.xml");
            var registry = new AuthenticationServiceRegistry(registryPath);
            var grants = new AuthenticationGrantStore(grantsPath);
            Directory.CreateDirectory(registryPath);
            Directory.CreateDirectory(grantsPath);
            Assert.ThrowsAny<IOException>(() => registry.AddConfirmed(endpoint));
            Assert.Empty(registry.Services);
            Assert.ThrowsAny<IOException>(() => grants.Set(
                    endpoint.Identity.Address,
                    endpoint,
                    AuthenticationNonce.Create(),
                    DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
                )
            );
            Assert.Null(grants.Find(endpoint.Identity.Address, endpoint));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
        }
    }

    private static AuthenticationServiceEndpoint Endpoint(ECDsa key, string url = "https://auth.example/")
    {
        return new AuthenticationServiceEndpoint(
            new AuthenticationServiceDescription(
                AuthenticationProtocol.Version,
                "Test",
                url,
                AuthenticationPublicKey.From(key).EncodedKey
            )
        );
    }
}
