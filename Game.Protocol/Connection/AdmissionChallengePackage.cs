using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record AdmissionChallengePackage(
    [property: Key(0)] ServerAdmissionMode Mode,
    [property: Key(1), MaxLength(172)] string Nonce,
    [property: Key(2)] long ExpiresAt,
    [property: Key(3), MaxLength(512)] string AuthorityName = "",
    [property: Key(4), MaxLength(8192)] string AuthorityUrl = "",
    [property: Key(5), MaxLength(1024)] string AuthorityPublicKey = ""
) : IProtocolMessage
{
}
