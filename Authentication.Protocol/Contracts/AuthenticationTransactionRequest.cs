namespace SCNET.Authentication.Protocol.Contracts;

public sealed record AuthenticationTransactionRequest(
    AuthenticationPurpose Purpose,
    AdmissionChallenge? Challenge = null,
    string? Grant = null
)
{
    public override string ToString() => "AuthenticationTransactionRequest { credentials redacted }";
}
