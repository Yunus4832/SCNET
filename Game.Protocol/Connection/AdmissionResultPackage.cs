namespace Game.Protocol.Connection;

public sealed record AdmissionResultPackage(
    string Nonce,
    AdmissionResultCode Code,
    int RetryAfterSeconds = 0
) : IProtocolMessage
{
    public byte ID => (byte)PackageType.AdmissionResult;
}
