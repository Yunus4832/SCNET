using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Engine.Network.Security;

using Microsoft.IdentityModel.Tokens;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Security;

namespace Authentication.Protocol.Test;

public sealed class AuthenticationProtocolTest
{
    private static readonly DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(1792840000);

    [Fact]
    public void PublicKeyAddressMatchesExistingTransportIdentity()
    {
        using var identity = NetworkIdentity.Create();
        var publicKey = new AuthenticationPublicKey(identity.PublicIdentity.PublicKey.Span);
        Assert.Equal(identity.PublicIdentity.Address, publicKey.Address);
        Assert.Equal(publicKey.Address, AuthenticationPublicKey.Parse(publicKey.EncodedKey).Address);
    }

    [Fact]
    public async Task ApprovalVerifiesWithOnlyThePinnedProviderPublicKey()
    {
        using var authority = CreateKey();
        var challenge = Challenge();
        var token = AdmissionApprovalTokens.Issue(authority, challenge, _now);
        var approved = await AdmissionApprovalTokens.ValidateAsync(
            token,
            AuthenticationPublicKey.From(authority),
            challenge,
            _now
        );
        Assert.Equal(challenge.Subject, approved.Subject);
        Assert.Equal(challenge.Server, approved.Server);
        Assert.Equal(challenge.ChannelBinding, approved.ChannelBinding);
        Assert.Equal(challenge.ExpiresAt, approved.ExpiresAt);
        Assert.DoesNotContain("grant", Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(token.Split('.')[1])));
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("server")]
    [InlineData("nonce")]
    [InlineData("binding")]
    [InlineData("expiry")]
    public async Task ApprovalCannotBeUsedForAnotherAdmissionAttempt(string changed)
    {
        using var authority = CreateKey();
        using var other = CreateKey();
        var challenge = Challenge();
        var token = AdmissionApprovalTokens.Issue(authority, challenge, _now);
        var altered = changed switch
        {
            "subject" => challenge with { Subject = AuthenticationPublicKey.From(other).Address },
            "server" => challenge with { Server = AuthenticationPublicKey.From(other).Address },
            "nonce" => challenge with { Nonce = AuthenticationNonce.Create() },
            "binding" => challenge with { ChannelBinding = AuthenticationNonce.Create() },
            _ => challenge with { ExpiresAt = challenge.ExpiresAt - 1 }
        };
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => AdmissionApprovalTokens.ValidateAsync(
                token,
                AuthenticationPublicKey.From(authority),
                altered,
                _now
            )
        );
    }

    [Theory]
    [InlineData("purpose")]
    [InlineData("issuer")]
    [InlineData("algorithm")]
    [InlineData("type")]
    [InlineData("future")]
    [InlineData("duration")]
    [InlineData("nbf")]
    [InlineData("key_url")]
    [InlineData("duplicate")]
    [InlineData("audience_array")]
    [InlineData("numeric_string")]
    public async Task EvenCorrectlySignedTokensMustMeetTheProtocolProfile(string changed)
    {
        using var authority = CreateKey();
        var challenge = Challenge();
        var token = AdmissionApprovalTokens.Issue(authority, challenge, _now);
        var rewritten = Rewrite(
            token,
            authority,
            (header, payload) =>
            {
                switch (changed)
                {
                    case "purpose": payload["purpose"] = "login"; break;
                    case "issuer": payload["iss"] = challenge.Subject; break;
                    case "algorithm": header["alg"] = "none"; break;
                    case "type": header["typ"] = AuthenticationProtocol.RequestTokenType; break;
                    case "future": payload["iat"] = _now.ToUnixTimeSeconds() + 10; break;
                    case "duration": payload["iat"] = _now.ToUnixTimeSeconds() - 300; break;
                    case "nbf": payload["nbf"] = _now.ToUnixTimeSeconds() - 1; break;
                    case "key_url": header["jku"] = "https://attacker.invalid/key"; break;
                    case "audience_array": payload["aud"] = new JsonArray(challenge.Server, challenge.Subject); break;
                    case "numeric_string": payload["iat"] = _now.ToUnixTimeSeconds().ToString(); break;
                }
            },
            changed == "duplicate"
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => AdmissionApprovalTokens.ValidateAsync(
                rewritten,
                AuthenticationPublicKey.From(authority),
                challenge,
                _now
            )
        );
    }

    [Fact]
    public async Task DifferentProviderAndTamperedSignatureAreRejected()
    {
        using var authority = CreateKey();
        using var other = CreateKey();
        var challenge = Challenge();
        var token = AdmissionApprovalTokens.Issue(authority, challenge, _now);
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => AdmissionApprovalTokens.ValidateAsync(
                token,
                AuthenticationPublicKey.From(other),
                challenge,
                _now
            )
        );
        var parts = token.Split('.');
        var signature = Base64UrlEncoder.DecodeBytes(parts[2]);
        signature[0] ^= 1;
        var tampered = $"{parts[0]}.{parts[1]}.{Base64UrlEncoder.Encode(signature)}";
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => AdmissionApprovalTokens.ValidateAsync(
                tampered,
                AuthenticationPublicKey.From(authority),
                challenge,
                _now
            )
        );
    }

    [Fact]
    public async Task SignedRequestBindsUserProviderEndpointAndData()
    {
        using var user = CreateKey();
        using var authority = CreateKey();
        var address = AuthenticationPublicKey.From(authority).Address;
        var endpoint = new Uri("https://auth.example/v1/transactions");
        var data = new AuthenticationTransactionRequest(AuthenticationPurpose.Join, Challenge(), "private-grant");
        var proof = AuthenticationRequestProofs.Create(user, address, endpoint, data, _now);
        var verified = await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
            proof,
            address,
            endpoint,
            _now
        );
        Assert.Equal(AuthenticationPublicKey.From(user).Address, verified.Subject);
        Assert.Equal(data, verified.Data);
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() =>
            AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
                proof,
                address,
                new Uri("https://auth.example/v1/grants/revoke"),
                _now
            )
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() =>
            AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
                proof,
                AuthenticationPublicKey.From(user).Address,
                endpoint,
                _now
            )
        );
        var otherKey = proof with { PublicKey = AuthenticationPublicKey.From(authority).EncodedKey };
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() =>
            AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionRequest>(
                otherKey,
                address,
                endpoint,
                _now
            )
        );
    }

    [Fact]
    public async Task ExpiredProofFailsAndReplayWindowDoesNotEvictLiveProofs()
    {
        using var user = CreateKey();
        using var authority = CreateKey();
        var address = AuthenticationPublicKey.From(authority).Address;
        var endpoint = new Uri("https://auth.example/v1/transactions/poll");
        var proof = AuthenticationRequestProofs.Create(
            user,
            address,
            endpoint,
            new AuthenticationTransactionCommand("transaction"),
            _now
        );
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() =>
            AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionCommand>(
                proof,
                address,
                endpoint,
                _now.AddSeconds(60)
            )
        );
        var verified = await AuthenticationRequestProofs.ValidateAsync<AuthenticationTransactionCommand>(
            proof,
            address,
            endpoint,
            _now
        );
        var cache = new AuthenticationReplayCache(1);
        Assert.True(cache.TryAccept(verified.Subject, verified.RequestId, verified.ExpiresAt, _now));
        Assert.False(cache.TryAccept(verified.Subject, verified.RequestId, verified.ExpiresAt, _now));
        Assert.False(cache.TryAccept(verified.Subject, AuthenticationNonce.Create(), verified.ExpiresAt, _now));
        Assert.True(
            cache.TryAccept(
                verified.Subject,
                AuthenticationNonce.Create(),
                verified.ExpiresAt + 60,
                _now.AddSeconds(60)
            )
        );
    }

    [Fact]
    public async Task BrowserChallengeMayOutliveTheShortLivedApproval()
    {
        using var authority = CreateKey();
        var challenge = Challenge() with { ExpiresAt = _now.AddMinutes(10).ToUnixTimeSeconds() };
        var token = AdmissionApprovalTokens.Issue(authority, challenge, _now);
        var approval = await AdmissionApprovalTokens.ValidateAsync(
            token,
            AuthenticationPublicKey.From(authority),
            challenge,
            _now
        );
        Assert.Equal(_now.AddMinutes(2).ToUnixTimeSeconds(), approval.ExpiresAt);
        await Assert.ThrowsAsync<AuthenticationProtocolException>(() => AdmissionApprovalTokens.ValidateAsync(
                token,
                AuthenticationPublicKey.From(authority),
                challenge,
                _now.AddMinutes(2)
            )
        );
    }

    [Fact]
    public void ChallengeLifetimeAllowsOnlyTheDeclaredClockSkew()
    {
        var challenge = Challenge() with
        {
            ExpiresAt = _now.ToUnixTimeSeconds() +
                        AuthenticationProtocol.MaximumChallengeLifetimeSeconds +
                        AuthenticationProtocol.ClockSkewSeconds
        };
        challenge.Validate(_now);
        Assert.Throws<ArgumentException>(() => (challenge with { ExpiresAt = challenge.ExpiresAt + 1 }).Validate(_now));
    }

    private static ECDsa CreateKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static AdmissionChallenge Challenge()
    {
        using var user = CreateKey();
        using var server = CreateKey();
        return new AdmissionChallenge(
            AuthenticationPublicKey.From(user).Address,
            AuthenticationPublicKey.From(server).Address,
            AuthenticationNonce.Create(),
            AuthenticationNonce.Create(),
            _now.ToUnixTimeSeconds() + 90
        );
    }

    private static string Rewrite(
        string token,
        ECDsa key,
        Action<JsonObject, JsonObject> change,
        bool duplicateSubject
    )
    {
        var parts = token.Split('.');
        var header = JsonNode.Parse(Base64UrlEncoder.DecodeBytes(parts[0]))!.AsObject();
        var payload = JsonNode.Parse(Base64UrlEncoder.DecodeBytes(parts[1]))!.AsObject();
        change(header, payload);
        var json = payload.ToJsonString();
        if (duplicateSubject) { json = json[..^1] + ",\"sub\":\"duplicate\"}"; }

        var input = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(header.ToJsonString())) +
                    "." +
                    Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        var signature = key.SignData(
            Encoding.ASCII.GetBytes(input),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation
        );
        return input + "." + Base64UrlEncoder.Encode(signature);
    }
}
