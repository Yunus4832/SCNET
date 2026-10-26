using Engine.Core.Security;

using SCNET.Authentication.Protocol.Security;

namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>One connection's random challenge; all fields are covered by the provider approval.</summary>
public sealed record AdmissionChallenge(
    string Subject,
    string Server,
    string Nonce,
    string ChannelBinding,
    long ExpiresAt
)
{
    public void Validate(DateTimeOffset now)
    {
        if (IdentityAddress.Normalize(Subject) != Subject || IdentityAddress.Normalize(Server) != Server)
        {
            throw new FormatException("Admission addresses must be canonical.");
        }

        AuthenticationNonce.Validate(Nonce);
        AuthenticationNonce.Validate(ChannelBinding);
        if (ExpiresAt <= now.ToUnixTimeSeconds() ||
            ExpiresAt >
            now.ToUnixTimeSeconds() +
            AuthenticationProtocol.MaximumChallengeLifetimeSeconds +
            AuthenticationProtocol.ClockSkewSeconds)
        {
            throw new ArgumentException("Admission challenge has expired or its lifetime is too long.");
        }
    }
}
