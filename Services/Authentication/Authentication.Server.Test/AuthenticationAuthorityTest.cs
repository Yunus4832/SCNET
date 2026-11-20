using System.Security.Cryptography;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;
using SCNET.Authentication.Server.Domain;

namespace Authentication.Server.Test;

public sealed class AuthenticationAuthorityTest
{
    private static readonly DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(1792840000);

    [Fact]
    public async Task BrowserLoginIssuesReusableSubjectGrantAndConnectionSpecificApproval()
    {
        using var provider = Key();
        using var player = Key();
        using var server = Key();
        var subject = AuthenticationPublicKey.From(player).Address;
        var authority = Authority(provider);
        var login = authority.Begin(subject, new AuthenticationTransactionRequest(AuthenticationPurpose.Login), _now);
        Assert.Equal(AuthenticationTransactionState.Pending, login.State);
        Assert.Null(login.Grant);
        var secret = new Uri(login.BrowserUrl!).Fragment[1..];
        Assert.True(authority.ApproveBrowser(login.TransactionId, secret, "account", _now.AddSeconds(1)));
        var complete = authority.Poll(subject, login.TransactionId, _now.AddSeconds(2));
        Assert.Equal(AuthenticationTransactionState.Completed, complete.State);
        Assert.NotNull(complete.Grant);
        Assert.Null(complete.Approval);
        var challenge = new AdmissionChallenge(
            subject,
            AuthenticationPublicKey.From(server).Address,
            AuthenticationNonce.Create(),
            AuthenticationNonce.Create(),
            _now.AddSeconds(90).ToUnixTimeSeconds()
        );
        var joined = authority.Begin(
            subject,
            new AuthenticationTransactionRequest(AuthenticationPurpose.Join, challenge, complete.Grant),
            _now.AddSeconds(3)
        );
        Assert.Equal(AuthenticationTransactionState.Completed, joined.State);
        Assert.Null(joined.BrowserUrl);
        Assert.Null(joined.Grant);
        Assert.NotNull(joined.Approval);
        var validated = await AdmissionApprovalTokens.ValidateAsync(
            joined.Approval,
            AuthenticationPublicKey.From(provider),
            challenge,
            _now.AddSeconds(3)
        );
        Assert.Equal(subject, validated.Subject);
        Assert.DoesNotContain(complete.Grant, System.Text.Json.JsonSerializer.Serialize(authority.State));
    }

    [Fact]
    public void CancelRejectsLateBrowserApprovalAndNeverReturnsGrant()
    {
        using var provider = Key();
        using var player = Key();
        var subject = AuthenticationPublicKey.From(player).Address;
        var authority = Authority(provider);
        var pending = authority.Begin(subject, new AuthenticationTransactionRequest(AuthenticationPurpose.Login), _now);
        var cancelled = authority.Cancel(subject, pending.TransactionId, _now);
        Assert.Equal(AuthenticationTransactionState.Cancelled, cancelled.State);
        Assert.False(
            authority.ApproveBrowser(
                pending.TransactionId,
                new Uri(pending.BrowserUrl!).Fragment[1..],
                "account",
                _now.AddSeconds(1)
            )
        );
        Assert.Null(authority.Poll(subject, pending.TransactionId, _now.AddSeconds(3)).Grant);
        Assert.Empty(authority.State.Grants);
    }

    [Theory]
    [InlineData("banned", AuthenticationError.SubjectBanned)]
    [InlineData("revoked", AuthenticationError.GrantRevoked)]
    [InlineData("expired", AuthenticationError.GrantExpired)]
    [InlineData("other_subject", AuthenticationError.GrantRevoked)]
    public void CachedGrantsCannotBypassAccountOrSubjectRules(string failure, AuthenticationError expected)
    {
        using var provider = Key();
        using var player = Key();
        using var other = Key();
        var subject = AuthenticationPublicKey.From(player).Address;
        var authority = Authority(provider);
        var transaction = authority.Begin(
            subject,
            new AuthenticationTransactionRequest(AuthenticationPurpose.Login),
            _now
        );
        Assert.True(
            authority.ApproveBrowser(
                transaction.TransactionId,
                new Uri(transaction.BrowserUrl!).Fragment[1..],
                "account",
                _now
            )
        );
        var result = authority.Poll(subject, transaction.TransactionId, _now);
        var now = _now.AddSeconds(3);
        switch (failure)
        {
            case "banned": authority.SetAccountBan("account", true); break;
            case "revoked": authority.Revoke(subject, result.Grant!); break;
            case "expired": now = _now.AddDays(31); break;
            case "other_subject": subject = AuthenticationPublicKey.From(other).Address; break;
        }

        var exception = Assert.Throws<AuthenticationRemoteException>(() => authority.Begin(
                subject,
                new AuthenticationTransactionRequest(AuthenticationPurpose.Login, Grant: result.Grant),
                now
            )
        );
        Assert.Equal(expected, exception.Error);
    }

    [Fact]
    public void OnlyTheProvenSubjectCanPollOrCancelAndOnlyTheBrowserSecretCanApprove()
    {
        using var provider = Key();
        using var player = Key();
        using var other = Key();
        var subject = AuthenticationPublicKey.From(player).Address;
        var stranger = AuthenticationPublicKey.From(other).Address;
        var authority = Authority(provider);
        var pending = authority.Begin(subject, new AuthenticationTransactionRequest(AuthenticationPurpose.Login), _now);
        Assert.Throws<AuthenticationRemoteException>(() => authority.Poll(stranger, pending.TransactionId, _now));
        Assert.Throws<AuthenticationRemoteException>(() => authority.Cancel(stranger, pending.TransactionId, _now));
        Assert.False(authority.ApproveBrowser(pending.TransactionId, AuthenticationNonce.Create(), "account", _now));
        var expired = authority.Poll(subject, pending.TransactionId, _now.AddMinutes(5));
        Assert.Equal(AuthenticationTransactionState.Expired, expired.State);
        Assert.False(
            authority.ApproveBrowser(
                pending.TransactionId,
                new Uri(pending.BrowserUrl!).Fragment[1..],
                "account",
                _now.AddMinutes(5)
            )
        );
    }

    private static ECDsa Key() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static AuthenticationAuthority Authority(ECDsa key)
    {
        return new(key, new ProviderAuthorizationState(), new Uri("https://auth.example/"));
    }
}
