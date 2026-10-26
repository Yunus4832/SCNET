namespace Game.Admission;

/// <summary>Trusted in-process admission uses the same policy; no UDP or world/player creation is involved.</summary>
internal sealed class LocalHostAdmissionSession(
    string worldDirectory,
    ServerAdmissionConfiguration configuration,
    string subject,
    string server,
    DateTimeOffset now
)
{
    private readonly AdmissionAttempt _attempt = new(
        configuration,
        subject,
        server,
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),
        now
    );

    private readonly string _policy = configuration.Fingerprint();

    private Task<AdmissionResultPackage>? _verification;

    internal bool Approved { get; private set; }

    internal SCNET.Authentication.Protocol.Contracts.AdmissionChallenge Challenge => _attempt.Challenge;

    internal AdmissionChallengePackage Describe() => _attempt.Describe();

    internal bool Submit(AdmissionProofPackage proof)
    {
        if (Approved || _verification is not null || _attempt.IsTerminal || proof.Nonce != Challenge.Nonce)
        {
            return false;
        }

        _verification = Task.Run(() => _attempt.AuthorizeAsync(proof, DateTimeOffset.UtcNow));
        _ = _verification.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        return true;
    }

    internal AdmissionResultPackage? Poll(DateTimeOffset timestamp)
    {
        if (Approved) { return null; }

        if (timestamp.ToUnixTimeSeconds() >= Challenge.ExpiresAt)
        {
            return new AdmissionResultPackage(Challenge.Nonce, AdmissionResultCode.Expired);
        }

        if (_verification is not { IsCompleted: true } task) { return null; }

        _verification = null;
        var result = task.IsCompletedSuccessfully
            ? task.Result
            : new AdmissionResultPackage(Challenge.Nonce, AdmissionResultCode.Denied);
        if (_attempt.IsTerminal &&
            result.Code is AdmissionResultCode.IncorrectPassword or AdmissionResultCode.ApprovalRejected)
        {
            result = result with { Code = AdmissionResultCode.Denied };
        }

        Approved = result.Code == AdmissionResultCode.Accepted;
        return result;
    }

    internal bool Authorizes(
        string directory,
        ServerAdmissionConfiguration policy,
        string player,
        string serverIdentity,
        DateTimeOffset timestamp
    )
    {
        return Approved &&
               timestamp.ToUnixTimeSeconds() < Challenge.ExpiresAt &&
               worldDirectory == directory &&
               subject == player &&
               server == serverIdentity &&
               _policy == policy.Fingerprint();
    }
}
