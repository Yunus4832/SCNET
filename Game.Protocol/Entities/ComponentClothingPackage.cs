using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(SkinAvailabilityReplyPackage))]
[Union(1, typeof(SkinAvailabilityRequestPackage))]
[Union(2, typeof(SkinReplyPackage))]
[Union(3, typeof(SkinRequestPackage))]
public abstract record ComponentClothingPackage : IProtocolMessage
{
    public enum DataType
    {
        RequestSkin = 0, //请求皮肤

        ReplySkin = 1, //回复皮肤

        WhoHas = 2, //向客户端请求资源

        WhoHasReply = 3 //客户回应请求
    }

    [IgnoreMember] public abstract DataType Type { get; }

    [Key(0)] public string SkinName { get; init; } = string.Empty;
}
