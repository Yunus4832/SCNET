namespace Game.Protocol.Connection;

public sealed record AdmissionProofPackage(
    string Nonce,
    string Password = "",
    string Approval = "",
    bool Cancel = false
) : IProtocolMessage
{
    public byte ID => (byte)PackageType.AdmissionProof;

    public override string ToString() => "AdmissionProofPackage { credentials redacted }";
}
