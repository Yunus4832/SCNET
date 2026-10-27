using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandRequestPackage : CommandPackage
{
    [IgnoreMember] public override CommandPackageMode Mode => CommandPackageMode.Request;

    [Key(1)] public ResourceId CommandId { get; init; }

    [Key(2), MaxLength(4096)] public byte[] Payload { get; init; } = [];
}
