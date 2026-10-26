using System.Security.Cryptography;

using Engine.Network;

using Game.Authentication;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Survivalcraft.Test.Authentication;

public sealed class TemporaryJoinAuthorizationTest
{
    [Fact]
    public void InMemoryRecoveryRequiresTheSamePlayerServerAuthorityEndpointAndTarget()
    {
        using var player = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var authority = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var endpoint = Endpoint(authority, "https://auth.example/");
        var scope = AuthenticationJoinScope.ForRemote(new AddressNetworkTarget("game.example", 28887));
        var now = DateTimeOffset.UtcNow;
        var subject = AuthenticationPublicKey.From(player).Address;
        var serverAddress = AuthenticationPublicKey.From(server).Address;
        var grant = new StoredAuthenticationGrant(
            subject,
            endpoint.Identity.Address,
            endpoint.BaseUri.AbsoluteUri,
            AuthenticationNonce.Create(),
            now.AddMinutes(1).ToUnixTimeSeconds()
        );
        var authorization = new TemporaryJoinAuthorization(subject, serverAddress, scope, endpoint, grant);
        authorization.Validate();
        var challenge = new AdmissionChallenge(
            subject,
            serverAddress,
            AuthenticationNonce.Create(),
            AuthenticationNonce.Create(),
            now.AddMinutes(1).ToUnixTimeSeconds()
        );
        Assert.True(authorization.Matches(challenge, endpoint, scope));
        Assert.False(
            authorization.Matches(
                challenge with { Subject = AuthenticationPublicKey.From(other).Address },
                endpoint,
                scope
            )
        );
        Assert.False(
            authorization.Matches(
                challenge with { Server = AuthenticationPublicKey.From(other).Address },
                endpoint,
                scope
            )
        );
        Assert.False(authorization.Matches(challenge, Endpoint(other, "https://auth.example/"), scope));
        Assert.False(authorization.Matches(challenge, Endpoint(authority, "https://moved.example/"), scope));
        Assert.False(
            authorization.Matches(
                challenge,
                endpoint,
                AuthenticationJoinScope.ForRemote(new AddressNetworkTarget("another.example", 28887))
            )
        );
        Assert.Same(grant, authorization.ValidGrant(now));
        Assert.Null(authorization.ValidGrant(now.AddMinutes(2)));
        Assert.DoesNotContain(grant.Secret, authorization.ToString());
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
