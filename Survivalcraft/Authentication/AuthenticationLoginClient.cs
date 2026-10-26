using System.Security.Cryptography;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;
using SCNET.Authentication.Protocol.Security;

namespace Game.Authentication;

/// <summary>One cancellable login operation. UI/trust decisions and persistent installation remain with the caller.</summary>
public sealed class AuthenticationLoginClient(
    AuthenticationGrantStore grants,
    Func<AuthenticationServiceEndpoint, AuthenticationHttpClient>? createClient = null
)
{
    public async Task<AuthenticationLoginResult> AuthenticateAsync(
        ECDsa subjectKey,
        AuthenticationServiceEndpoint endpoint,
        AdmissionChallenge? challenge,
        bool useCachedGrant,
        Func<Uri, CancellationToken, Task> openBrowser,
        CancellationToken cancellationToken,
        StoredAuthenticationGrant? temporaryGrant = null
    )
    {
        var subject = AuthenticationPublicKey.From(subjectKey).Address;
        if (challenge is not null && challenge.Subject != subject)
        {
            throw new AuthenticationProtocolException("Admission challenge belongs to another user identity.");
        }

        if (temporaryGrant is { } transient &&
            (transient.Subject != subject ||
             transient.Authority != endpoint.Identity.Address ||
             transient.ServiceUrl != endpoint.BaseUri.AbsoluteUri ||
             transient.Secret.Length is < 1 or > 4096 ||
             transient.Secret.Any(char.IsControl) ||
             transient.ExpiresAt <= 0 ||
             transient.ExpiresAt > DateTimeOffset.MaxValue.ToUnixTimeSeconds()))
        {
            throw new AuthenticationProtocolException("Temporary grant belongs to another context or is invalid.");
        }

        var cached = temporaryGrant ?? (useCachedGrant ? grants.Find(subject, endpoint) : null);
        if (cached is not null && !cached.IsValid(DateTimeOffset.UtcNow))
        {
            if (temporaryGrant is null) { grants.Remove(subject, endpoint.Identity.Address); }

            cached = null;
        }

        using var client = createClient?.Invoke(endpoint) ?? new AuthenticationHttpClient(endpoint);
        await client.VerifyDiscoveryAsync(cancellationToken);
        string? transactionId = null;
        var completed = false;
        try
        {
            var request = new AuthenticationTransactionRequest(
                challenge is null ? AuthenticationPurpose.Login : AuthenticationPurpose.Join,
                challenge,
                cached?.Secret
            );
            AuthenticationTransactionResult transaction;
            try { transaction = await client.CreateTransactionAsync(subjectKey, request, cancellationToken); }
            catch (AuthenticationRemoteException error) when (cached is not null &&
                                                              error.Error is AuthenticationError.GrantExpired
                                                                  or AuthenticationError.GrantRevoked)
            {
                if (temporaryGrant is null) { grants.Remove(subject, endpoint.Identity.Address); }

                cached = null;
                transaction = await client.CreateTransactionAsync(
                    subjectKey,
                    request with { Grant = null },
                    cancellationToken
                );
            }

            ValidateTransaction(transaction, null);
            transactionId = transaction.TransactionId;
            var expiresAt = transaction.ExpiresAt;
            if (transaction.State == AuthenticationTransactionState.Pending)
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt)
                {
                    throw new AuthenticationRemoteException(AuthenticationError.Expired);
                }

                var browser = ValidateBrowserUrl(transaction.BrowserUrl, endpoint);
                cancellationToken.ThrowIfCancellationRequested();
                await openBrowser(browser, cancellationToken);
            }

            while (transaction.State == AuthenticationTransactionState.Pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt)
                {
                    throw new AuthenticationRemoteException(AuthenticationError.Expired);
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(transaction.PollAfterSeconds, 1, 30)),
                    cancellationToken
                );
                try { transaction = await client.PollAsync(subjectKey, transactionId, cancellationToken); }
                catch (AuthenticationRemoteException error) when (error.Error == AuthenticationError.RateLimited)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(Math.Clamp(error.RetryAfterSeconds, 1, 30)),
                        cancellationToken
                    );
                    continue;
                }

                catch (Exception error) when (error is HttpRequestException ||
                                              error is TaskCanceledException &&
                                              !cancellationToken.IsCancellationRequested)
                {
                    // A mobile browser may temporarily suspend app networking. Keep this transaction bounded
                    // by its existing deadline instead of abandoning an approval completed in the browser.
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                ValidateTransaction(transaction, transactionId);
                if (transaction.ExpiresAt != expiresAt)
                {
                    throw new AuthenticationProtocolException(
                        "Authentication service changed the transaction deadline."
                    );
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (transaction.State != AuthenticationTransactionState.Completed ||
                transaction.Error != AuthenticationError.None)
            {
                throw new AuthenticationRemoteException(
                    transaction.Error != AuthenticationError.None
                        ? transaction.Error
                        : transaction.State switch
                        {
                            AuthenticationTransactionState.Cancelled => AuthenticationError.Cancelled,
                            AuthenticationTransactionState.Expired => AuthenticationError.Expired,
                            _ => AuthenticationError.Denied
                        }
                );
            }

            if (challenge is not null)
            {
                if (string.IsNullOrEmpty(transaction.Approval))
                {
                    throw new AuthenticationProtocolException("Admission completed without a signed approval.");
                }

                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= challenge.ExpiresAt)
                {
                    throw new AuthenticationRemoteException(AuthenticationError.Expired);
                }

                await AdmissionApprovalTokens.ValidateAsync(
                    transaction.Approval,
                    endpoint.Identity,
                    challenge,
                    DateTimeOffset.UtcNow
                );
            }
            else if (transaction.Approval is not null)
            {
                throw new AuthenticationProtocolException("Pre-login cannot return a game admission approval.");
            }

            StoredAuthenticationGrant? grant = null;
            if (transaction.Grant is { } secret)
            {
                if (secret.Length is < 1 or > 4096 ||
                    secret.Any(char.IsControl) ||
                    transaction.GrantExpiresAt is not { } expiry ||
                    expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                    expiry > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
                {
                    throw new AuthenticationProtocolException("Invalid authentication grant metadata.");
                }

                grant = new StoredAuthenticationGrant(
                    subject,
                    endpoint.Identity.Address,
                    endpoint.BaseUri.AbsoluteUri,
                    secret,
                    expiry
                );
            }
            else
            {
                grant = cached;
                if (grant is not null && transaction.GrantExpiresAt is { } expires)
                {
                    if (expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                        expires > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
                    {
                        throw new AuthenticationProtocolException("Invalid cached grant lifetime.");
                    }

                    grant = grant with { ExpiresAt = expires };
                }
            }

            if (challenge is null && grant is null)
            {
                throw new AuthenticationProtocolException("Pre-login completed without a reusable authorization.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            completed = true;
            return new AuthenticationLoginResult(grant, transaction.Approval);
        }
        finally
        {
            if (!completed && transactionId is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await client.CancelAsync(subjectKey, transactionId, cleanup.Token); }
                catch (Exception error) when (error is HttpRequestException or TaskCanceledException
                                                  or AuthenticationProtocolException or AuthenticationRemoteException)
                {
                    // Best-effort remote cleanup; the cancelled operation never publishes credentials locally.
                }
            }
        }
    }

    private static Uri ValidateBrowserUrl(string? value, AuthenticationServiceEndpoint endpoint)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.UserInfo.Length > 0 ||
            uri.Host != endpoint.BaseUri.Host ||
            uri.Port != endpoint.BaseUri.Port ||
            value!.Length > 4096)
        {
            throw new AuthenticationProtocolException("Authentication browser entry is outside the confirmed service.");
        }

        return uri;
    }

    private static void ValidateTransaction(AuthenticationTransactionResult transaction, string? expectedId)
    {
        if (string.IsNullOrEmpty(transaction.TransactionId) ||
            transaction.TransactionId.Length > 128 ||
            transaction.TransactionId.Any(char.IsControl) ||
            expectedId is not null && transaction.TransactionId != expectedId ||
            !Enum.IsDefined(transaction.State) ||
            !Enum.IsDefined(transaction.Error) ||
            transaction.PollAfterSeconds is < 0 or > 30 ||
            transaction.ExpiresAt <= 0 ||
            transaction.ExpiresAt >
            DateTimeOffset.UtcNow.AddMinutes(10).AddSeconds(AuthenticationProtocol.ClockSkewSeconds)
                .ToUnixTimeSeconds() ||
            transaction.State == AuthenticationTransactionState.Pending &&
            (transaction.Error != AuthenticationError.None ||
             transaction.Grant is not null ||
             transaction.Approval is not null))
        {
            throw new AuthenticationProtocolException("Invalid authentication transaction response.");
        }
    }
}
