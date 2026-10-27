using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record AdmissionResultPackage(
    [property: Key(0), MaxLength(172)] string Nonce,
    [property: Key(1)] AdmissionResultCode Code,
    [property: Key(2)] int RetryAfterSeconds = 0
) : IProtocolMessage
{
}
