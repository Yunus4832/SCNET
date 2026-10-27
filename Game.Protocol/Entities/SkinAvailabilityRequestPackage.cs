using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SkinAvailabilityRequestPackage : ComponentClothingPackage
{
    [IgnoreMember] public override DataType Type => DataType.WhoHas;
}
