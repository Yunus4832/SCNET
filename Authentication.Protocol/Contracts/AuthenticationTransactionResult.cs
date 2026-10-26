namespace SCNET.Authentication.Protocol.Contracts;

public sealed record AuthenticationTransactionResult(
    string TransactionId,
    AuthenticationTransactionState State,
    long ExpiresAt,
    int PollAfterSeconds,
    string? BrowserUrl = null,
    string? Grant = null,
    long? GrantExpiresAt = null,
    string? Approval = null,
    AuthenticationError Error = AuthenticationError.None
)
{
    public override string ToString() => "AuthenticationTransactionResult { credentials redacted }";
}
