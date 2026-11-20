using System.Security.Cryptography;
using System.Text;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace SCNET.Authentication.Server.Domain;

/// <summary>Provider authorization aggregate: transaction completion, grants, and provider account bans.</summary>
public sealed class AuthenticationAuthority
{
    private const int _maximumTransactions = 1000;

    private readonly Dictionary<string, AuthenticationTransaction> _transactions = new(StringComparer.Ordinal);

    private readonly Dictionary<string, long> _lastPoll = new(StringComparer.Ordinal);

    private readonly ECDsa _identity;

    private readonly ProviderAuthorizationState _state;

    private readonly Uri _baseUri;

    public AuthenticationAuthority(ECDsa identity, ProviderAuthorizationState state, Uri baseUri)
    {
        _identity = identity;
        if (state.Grants.Count > 10000 || state.BannedAccounts.Count > 10000)
        {
            throw new InvalidDataException("Provider authorization state exceeds supported limits.");
        }

        foreach (var (hash, grant) in state.Grants)
        {
            if (hash.Length != 64 ||
                Convert.ToHexString(Convert.FromHexString(hash)) != hash ||
                Engine.Core.Security.IdentityAddress.Normalize(grant.Subject) != grant.Subject ||
                !IsValidAccount(grant.Account) ||
                grant.ExpiresAt <= 0)
            {
                throw new InvalidDataException("Invalid persisted provider grant.");
            }
        }

        if (state.BannedAccounts.Any(account => !IsValidAccount(account)))
        {
            throw new InvalidDataException("Invalid persisted provider account ban.");
        }

        _state = new ProviderAuthorizationState
        {
            Grants = new Dictionary<string, ProviderGrant>(state.Grants, StringComparer.Ordinal),
            BannedAccounts = new HashSet<string>(state.BannedAccounts, StringComparer.Ordinal)
        };
        _baseUri = AuthenticationServiceEndpoint.ParseBaseUri(baseUri.AbsoluteUri);
    }

    public ProviderAuthorizationState State => new()
    {
        Grants = new Dictionary<string, ProviderGrant>(_state.Grants, StringComparer.Ordinal),
        BannedAccounts = new HashSet<string>(_state.BannedAccounts, StringComparer.Ordinal)
    };

    public AuthenticationTransactionResult Begin(
        string subject,
        AuthenticationTransactionRequest request,
        DateTimeOffset now
    )
    {
        ValidateRequest(subject, request, now);
        Prune(now);
        if (_transactions.Count >= _maximumTransactions ||
            _transactions.Values.Count(transaction => transaction.Subject == subject) >= 8)
        {
            throw new AuthenticationRemoteException(AuthenticationError.RateLimited, 30);
        }

        var cachedGrant = request.Grant is not null ? RequireGrant(request.Grant, subject, now) : null;

        var browserSecret = AuthenticationNonce.Create();
        var expiresAt = request.Challenge?.ExpiresAt ?? now.AddMinutes(5).ToUnixTimeSeconds();
        var transaction = new AuthenticationTransaction(
            AuthenticationNonce.Create(),
            subject,
            HashSecret(browserSecret),
            request,
            expiresAt
        );
        _transactions.Add(transaction.Id, transaction);
        if (request.Grant is not null)
        {
            transaction.Complete(null, cachedGrant!.ExpiresAt, IssueApproval(request, now));
            return transaction.Capture();
        }

        return transaction.Capture(new Uri(_baseUri, $"login/{transaction.Id}#{browserSecret}").AbsoluteUri);
    }

    public AuthenticationTransactionResult Poll(string subject, string transactionId, DateTimeOffset now)
    {
        var transaction = Find(subject, transactionId, now);
        var timestamp = now.ToUnixTimeSeconds();
        if (_lastPoll.TryGetValue(transactionId, out var last) && timestamp - last < 2)
        {
            throw new AuthenticationRemoteException(AuthenticationError.RateLimited, 2);
        }

        _lastPoll[transactionId] = timestamp;
        return transaction.Capture();
    }

    public AuthenticationTransactionResult Cancel(string subject, string transactionId, DateTimeOffset now)
    {
        var transaction = Find(subject, transactionId, now);
        if (transaction.Grant is { } secret && _state.Grants.TryGetValue(HashSecret(secret), out var grant))
        {
            _state.Grants[HashSecret(secret)] = grant with { Revoked = true };
        }

        transaction.Cancel();
        return transaction.Capture();
    }

    public bool ApproveBrowser(string transactionId, string browserSecret, string account, DateTimeOffset now)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction)) { return false; }

        transaction.Expire(now.ToUnixTimeSeconds());
        if (transaction.State != AuthenticationTransactionState.Pending ||
            !SecretMatches(browserSecret, transaction.BrowserSecretHash)) { return false; }

        if (!IsValidAccount(account)) { return false; }

        if (_state.BannedAccounts.Contains(account))
        {
            transaction.Deny(AuthenticationError.SubjectBanned);
            return false;
        }

        foreach (var item in _state.Grants.Where(item => item.Value.Revoked ||
                                                         item.Value.ExpiresAt <= now.ToUnixTimeSeconds()
                 ).ToArray())
        {
            _state.Grants.Remove(item.Key);
        }

        if (_state.Grants.Count >= 10000)
        {
            throw new AuthenticationRemoteException(AuthenticationError.RateLimited, 60);
        }

        var secret = AuthenticationNonce.Create();
        var expiresAt = now.AddDays(30).ToUnixTimeSeconds();
        _state.Grants.Add(HashSecret(secret), new ProviderGrant(transaction.Subject, account, expiresAt));
        transaction.Complete(secret, expiresAt, IssueApproval(transaction.Request, now));
        return true;
    }

    public void Revoke(string subject, string secret)
    {
        var hash = HashSecret(secret);
        if (!_state.Grants.TryGetValue(hash, out var grant) || grant.Subject != subject)
        {
            throw new AuthenticationRemoteException(AuthenticationError.NotFound);
        }

        _state.Grants[hash] = grant with { Revoked = true };
    }

    public void SetAccountBan(string account, bool banned)
    {
        if (!IsValidAccount(account)) { throw new ArgumentException("Account is invalid.", nameof(account)); }

        if (banned) { _state.BannedAccounts.Add(account); }
        else { _state.BannedAccounts.Remove(account); }
    }

    public string? GetBrowserSubject(string transactionId)
    {
        return _transactions.GetValueOrDefault(transactionId)?.Subject;
    }

    private ProviderGrant RequireGrant(string secret, string subject, DateTimeOffset now)
    {
        if (!_state.Grants.TryGetValue(HashSecret(secret), out var grant) || grant.Subject != subject)
        {
            throw new AuthenticationRemoteException(AuthenticationError.GrantRevoked);
        }

        if (_state.BannedAccounts.Contains(grant.Account))
        {
            throw new AuthenticationRemoteException(AuthenticationError.SubjectBanned);
        }

        if (grant.Revoked) { throw new AuthenticationRemoteException(AuthenticationError.GrantRevoked); }

        if (grant.ExpiresAt <= now.ToUnixTimeSeconds())
        {
            throw new AuthenticationRemoteException(AuthenticationError.GrantExpired);
        }

        return grant;
    }

    private string? IssueApproval(AuthenticationTransactionRequest request, DateTimeOffset now)
    {
        return request.Purpose == AuthenticationPurpose.Join
            ? AdmissionApprovalTokens.Issue(_identity, request.Challenge!, now)
            : null;
    }

    private AuthenticationTransaction Find(string subject, string id, DateTimeOffset now)
    {
        if (!_transactions.TryGetValue(id, out var transaction) || transaction.Subject != subject)
        {
            throw new AuthenticationRemoteException(AuthenticationError.NotFound);
        }

        transaction.Expire(now.ToUnixTimeSeconds());
        return transaction;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var item in _transactions.Where(item => item.Value.ExpiresAt <= now.ToUnixTimeSeconds()).ToArray())
        {
            _transactions.Remove(item.Key);
            _lastPoll.Remove(item.Key);
        }
    }

    private static void ValidateRequest(string subject, AuthenticationTransactionRequest request, DateTimeOffset now)
    {
        if (!Enum.IsDefined(request.Purpose) ||
            request.Purpose == AuthenticationPurpose.Login && request.Challenge is not null ||
            request.Purpose == AuthenticationPurpose.Join && request.Challenge is null ||
            request.Grant is { Length: > 128 })
        {
            throw new AuthenticationRemoteException(AuthenticationError.InvalidRequest);
        }

        if (request.Challenge is { } challenge)
        {
            challenge.Validate(now);
            if (challenge.Subject != subject)
            {
                throw new AuthenticationRemoteException(AuthenticationError.InvalidRequest);
            }
        }
    }

    private static bool IsValidAccount(string account)
    {
        return !string.IsNullOrWhiteSpace(account) && account.Length <= 128 && !account.Any(char.IsControl);
    }

    private static string HashSecret(string secret)
    {
        AuthenticationNonce.Validate(secret);
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
    }

    private static bool SecretMatches(string secret, string expectedHash)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(HashSecret(secret)),
                Convert.FromHexString(expectedHash)
            );
        }
        catch (FormatException) { return false; }
    }
}
