using Game.Protocol.Identity;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandPermissionSnapshotPackage : CommandPackage
{
    [IgnoreMember] public override CommandPackageMode Mode => CommandPackageMode.PermissionSnapshot;

    [Key(1)] public PlayerAddress? PlayerAddress { get; init; }

    [Key(2), MaxLength(256)] public IReadOnlyList<CommandPermissionGrant> PermissionGrants { get; init; } = [];
}
