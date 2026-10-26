namespace Game.Protocol.Connection;

public sealed record AdmissionChallengePackage(
    ServerAdmissionMode Mode,
    string Nonce,
    long ExpiresAt,
    string AuthorityName = "",
    string AuthorityUrl = "",
    string AuthorityPublicKey = ""
) : IProtocolMessage
{
    public byte ID => (byte)PackageType.AdmissionChallenge;
}
