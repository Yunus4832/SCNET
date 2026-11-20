using SCNET.Authentication.Protocol.Contracts;

namespace SCNET.Authentication.Server.Domain;

internal sealed class AuthenticationTransaction(
    string id,
    string subject,
    string browserSecretHash,
    AuthenticationTransactionRequest request,
    long expiresAt
)
{
    public string Id { get; } = id;

    public string Subject { get; } = subject;

    public string BrowserSecretHash { get; } = browserSecretHash;

    public AuthenticationTransactionRequest Request { get; } = request;

    public long ExpiresAt { get; } = expiresAt;

    public AuthenticationTransactionState State { get; private set; } = AuthenticationTransactionState.Pending;

    public AuthenticationError Error { get; private set; }

    public string? Grant { get; private set; }

    public long? GrantExpiresAt { get; private set; }

    public string? Approval { get; private set; }

    public void Complete(string? grant, long? grantExpiresAt, string? approval)
    {
        if (State != AuthenticationTransactionState.Pending)
        {
            throw new InvalidOperationException("Transaction is terminal.");
        }

        Grant = grant;
        GrantExpiresAt = grantExpiresAt;
        Approval = approval;
        State = AuthenticationTransactionState.Completed;
    }

    public void Expire(long now)
    {
        if (now >= ExpiresAt)
        {
            State = AuthenticationTransactionState.Expired;
            Error = AuthenticationError.Expired;
            ClearSecrets();
        }
    }

    public void Cancel()
    {
        if (State == AuthenticationTransactionState.Expired) { return; }

        State = AuthenticationTransactionState.Cancelled;
        Error = AuthenticationError.Cancelled;
        ClearSecrets();
    }

    public void Deny(AuthenticationError error)
    {
        if (State != AuthenticationTransactionState.Pending) { return; }

        State = AuthenticationTransactionState.Denied;
        Error = error;
        ClearSecrets();
    }

    public AuthenticationTransactionResult Capture(string? browserUrl = null)
    {
        return new(
            Id,
            State,
            ExpiresAt,
            2,
            browserUrl,
            Grant,
            GrantExpiresAt,
            Approval,
            Error
        );
    }

    private void ClearSecrets()
    {
        Grant = null;
        GrantExpiresAt = null;
        Approval = null;
    }
}
