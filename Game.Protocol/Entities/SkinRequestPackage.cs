using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SkinRequestPackage : ComponentClothingPackage
{
    [IgnoreMember] public override DataType Type => DataType.RequestSkin;
}
