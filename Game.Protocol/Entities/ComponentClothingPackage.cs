namespace Game.Protocol.Entities;

public sealed record ComponentClothingPackage : IProtocolMessage
{
    public enum DataType
    {
        RequestSkin = 0, //请求皮肤

        ReplySkin = 1, //回复皮肤

        WhoHas = 2, //向客户端请求资源

        WhoHasReply = 3 //客户回应请求
    }

    public byte[] SkinData { get; init; } = [];

    public DataType Type { get; init; }

    public string SkinName { get; init; } = string.Empty;

    public byte ID => (byte)PackageType.ComponentClothing;

    public ComponentClothingPackage()
    {
    }

    public ComponentClothingPackage(string skinName, DataType type)
    {
        SkinName = skinName;
        Type = type;
    }
}
