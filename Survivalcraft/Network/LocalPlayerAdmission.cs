using EntitySystem.Core;

using Game.Admission;

namespace Game.Network;

/// <summary>A pending in-process player join, bound to one loaded world and the common admission attempt.</summary>
internal sealed class LocalPlayerAdmission(Project world, AdmissionAttempt attempt)
{
    internal Project World { get; } = world;

    private Task<AdmissionResultPackage>? _verification;

    internal bool Approved { get; private set; }

    internal SCNET.Authentication.Protocol.Contracts.AdmissionChallenge Challenge => attempt.Challenge;

    internal AdmissionChallengePackage Describe() => attempt.Describe();

    internal bool Submit(AdmissionProofPackage proof)
    {
        if (Approved || _verification is not null || attempt.IsTerminal || proof.Nonce != Challenge.Nonce)
        {
            return false;
        }

        _verification = Task.Run(() => attempt.AuthorizeAsync(proof, DateTimeOffset.UtcNow));
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
        if (attempt.IsTerminal &&
            result.Code is AdmissionResultCode.IncorrectPassword or AdmissionResultCode.ApprovalRejected)
        {
            result = result with { Code = AdmissionResultCode.Denied };
        }

        Approved = result.Code == AdmissionResultCode.Accepted;
        return result;
    }
}
