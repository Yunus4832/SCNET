using System.Security.Cryptography;

using SCNET.Authentication.Protocol;
using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Security;

namespace Game.Admission;

/// <summary>One-use connection authorization; never creates world players or changes role ownership.</summary>
public sealed class AdmissionAttempt
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly ServerAdmissionPolicy _configuration;

    private bool _completed;

    private long _nextAttempt;

    private int _failures;

    public AdmissionAttempt(
        ServerAdmissionPolicy configuration,
        string subject,
        string server,
        ReadOnlySpan<byte> channelBinding,
        DateTimeOffset now
    )
    {
        _configuration = configuration;
        Challenge = new AdmissionChallenge(
            subject,
            server,
            AuthenticationNonce.Create(),
            AuthenticationNonce.Encode(channelBinding),
            now.AddSeconds(AuthenticationProtocol.MaximumChallengeLifetimeSeconds).ToUnixTimeSeconds()
        );
        Challenge.Validate(now);
    }

    public AdmissionChallenge Challenge { get; }

    internal bool IsTerminal => Volatile.Read(ref _completed);

    internal int RetryAfterSeconds(DateTimeOffset now)
    {
        return (int)Math.Max(0, Interlocked.Read(ref _nextAttempt) - now.ToUnixTimeSeconds());
    }

    public AdmissionChallengePackage Describe()
    {
        return new AdmissionChallengePackage(
            _configuration.Mode,
            Challenge.Nonce,
            Challenge.ExpiresAt,
            _configuration.Authority?.Description.DisplayName ?? string.Empty,
            _configuration.Authority?.Description.ServiceUrl ?? string.Empty,
            _configuration.Authority?.Description.PublicKey ?? string.Empty
        );
    }

    public async Task<AdmissionResultPackage> AuthorizeAsync(
        AdmissionProofPackage proof,
        DateTimeOffset now,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var timestamp = now.ToUnixTimeSeconds();
            if (_completed || proof.Nonce != Challenge.Nonce) { return Result(AdmissionResultCode.Denied); }

            if (timestamp >= Challenge.ExpiresAt)
            {
                _completed = true;
                return Result(AdmissionResultCode.Expired);
            }

            if (proof.Cancel)
            {
                _completed = true;
                return Result(AdmissionResultCode.Cancelled);
            }

            if (timestamp < _nextAttempt)
            {
                return Result(AdmissionResultCode.RateLimited, (int)(_nextAttempt - timestamp));
            }

            var accepted = false;
            var failure = AdmissionResultCode.Denied;
            switch (_configuration.Mode)
            {
                case ServerAdmissionMode.Open:
                    accepted = proof.Password.Length == 0 && proof.Approval.Length == 0;
                    break;
                case ServerAdmissionMode.Password:
                    accepted = proof.Approval.Length == 0 && _configuration.VerifyPassword(proof.Password);
                    failure = AdmissionResultCode.IncorrectPassword;
                    break;
                case ServerAdmissionMode.External:
                    failure = AdmissionResultCode.ApprovalRejected;
                    if (proof.Password.Length == 0 && proof.Approval.Length > 0)
                    {
                        try
                        {
                            await AdmissionApprovalTokens.ValidateAsync(
                                proof.Approval,
                                _configuration.Authority!.Identity,
                                Challenge,
                                now
                            );
                            accepted = true;
                        }
                        catch (Exception exception) when (exception is AuthenticationProtocolException or
                                                              ArgumentException or FormatException
                                                              or CryptographicException)
                        {
                            // Keep untrusted approval content and credential details out of diagnostics.
                        }
                    }

                    break;
            }

            if (accepted)
            {
                _completed = true;
                return Result(AdmissionResultCode.Accepted);
            }

            _failures++;
            _nextAttempt = timestamp + Math.Min(30, 1 << _failures);
            if (_failures >= 5) { _completed = true; }

            return Result(failure, (int)(_nextAttempt - timestamp));
        }
        finally { _gate.Release(); }
    }

    private AdmissionResultPackage Result(AdmissionResultCode code, int retry = 0) => new(Challenge.Nonce, code, retry);
}
