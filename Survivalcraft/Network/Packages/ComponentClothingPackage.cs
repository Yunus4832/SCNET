using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentClothingPackage : IPackage
{
    public enum DataType
    {
        RequestSkin, //请求皮肤
        ReplySkin, //回复皮肤
        WhoHas, //向客户端请求资源
        WhoHasReply //客户回应请求
    }

    public byte[] SkinData = [];

    public DataType Type;

    public string SkinName = string.Empty;

    public byte ID => (byte)PackageType.ComponentClothing;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;


    public ComponentClothingPackage()
    {
    }

    public ComponentClothingPackage(string skinName, DataType type)
    {
        Type = type;
        SkinName = skinName;
        if (!CharacterSkinsManager.HasSkinRes(SkinName))
        {
            return;
        }

        using var stream =
            Storage.OpenFile(Storage.CombinePaths(ModsManager.CharacterSkinsDirectoryName, skinName),
                OpenFileMode.Read);
        SkinData = ModsManager.StreamToBytes(stream);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case DataType.WhoHas:
            case DataType.RequestSkin:
                writer.Write(SkinName);
                break;
            case DataType.WhoHasReply:
            case DataType.ReplySkin:
                writer.Write(SkinName);
                writer.Write(SkinData.Length);
                writer.Write(SkinData);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<DataType>();
        switch (Type)
        {
            case DataType.WhoHas:
            case DataType.RequestSkin:
                SkinName = reader.ReadString();
                break;
            case DataType.WhoHasReply:
            case DataType.ReplySkin:
                SkinName = reader.ReadString();
                var len = reader.ReadInt32();
                SkinData = reader.ReadBytes(len);
                break;
        }
    }


}
