using MessagePack;

namespace Game.Protocol.Commands;

[MessagePackObject]
public sealed record CommandResultPackage : CommandPackage
{
    [IgnoreMember] public override CommandPackageMode Mode => CommandPackageMode.Result;

    [Key(1)] public CommandResponse? Result { get; init; }
}
