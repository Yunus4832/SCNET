using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SkinAvailabilityReplyPackage : ComponentClothingPackage
{
    [IgnoreMember] public override DataType Type => DataType.WhoHasReply;

    [Key(1)] public byte[] SkinData { get; init; } = [];
}
