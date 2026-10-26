namespace SCNET.Authentication.Protocol;

public static class AuthenticationProtocol
{
    public const int Version = 1;

    public const string DiscoveryPath = ".well-known/scnet-authentication";

    public const string TransactionsPath = "v1/transactions";

    public const string PollPath = "v1/transactions/poll";

    public const string CancelPath = "v1/transactions/cancel";

    public const string RevokePath = "v1/grants/revoke";

    public const string RequestTokenType = "scnet-auth-request+jwt";

    public const string ApprovalTokenType = "scnet-admission+jwt";

    public const int MaximumTokenLength = 16384;

    public const int MaximumRequestLifetimeSeconds = 60;

    public const int MaximumApprovalLifetimeSeconds = 120;

    public const int MaximumChallengeLifetimeSeconds = 600;

    public const int ClockSkewSeconds = 5;
}
