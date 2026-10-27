using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SkinReplyPackage : ComponentClothingPackage
{
    [IgnoreMember] public override DataType Type => DataType.ReplySkin;

    [Key(1)] public byte[] SkinData { get; init; } = [];
}
