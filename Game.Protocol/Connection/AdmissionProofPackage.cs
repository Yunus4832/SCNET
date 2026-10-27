using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record AdmissionProofPackage(
    [property: Key(0), MaxLength(172)] string Nonce,
    [property: Key(1), MaxLength(4096)] string Password = "",
    [property: Key(2), MaxLength(65536)] string Approval = "",
    [property: Key(3)] bool Cancel = false
) : IProtocolMessage
{
    public override string ToString() => "AdmissionProofPackage { credentials redacted }";
}
