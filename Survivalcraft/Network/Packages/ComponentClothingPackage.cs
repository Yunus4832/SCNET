using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentClothingPackage : IPackage
{
    public enum DataType
    {
        RequestSkin, //请求皮肤
        ReplySkin, //回复皮肤
        WhoHas, //向客户端请求资源
        WhoHasReply //客户回应请求
    }

    private byte[] _skinData = [];

    private DataType _type;

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
        _type = type;
        SkinName = skinName;
        if (!CharacterSkinsManager.HasSkinRes(SkinName))
        {
            return;
        }

        using var stream =
            Storage.OpenFile(Storage.CombinePaths(ModsManager.CharacterSkinsDirectoryName, skinName),
                OpenFileMode.Read);
        _skinData = ModsManager.StreamToBytes(stream);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case DataType.WhoHas:
            case DataType.RequestSkin:
                writer.Write(SkinName);
                break;
            case DataType.WhoHasReply:
            case DataType.ReplySkin:
                writer.Write(SkinName);
                writer.Write(_skinData.Length);
                writer.Write(_skinData);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<DataType>();
        switch (_type)
        {
            case DataType.WhoHas:
            case DataType.RequestSkin:
                SkinName = reader.ReadString();
                break;
            case DataType.WhoHasReply:
            case DataType.ReplySkin:
                SkinName = reader.ReadString();
                var len = reader.ReadInt32();
                _skinData = reader.ReadBytes(len);
                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        switch (_type)
        {
            case DataType.RequestSkin:
                if (CharacterSkinsManager.HasSkinRes(SkinName))
                {
                    netNode.QueuePackage(new ComponentClothingPackage(SkinName, DataType.ReplySkin));
                }
                else
                {
                    if (!CharacterSkinsManager.WaitReplyList.Contains(SkinName))
                    {
                        netNode.QueuePackage(new ComponentClothingPackage(SkinName, DataType.WhoHas));
                        CharacterSkinsManager.WaitReplyList.Add(SkinName);
                    }
                }

                break;
            //储存回复的资源
            case DataType.WhoHasReply:
            case DataType.ReplySkin:
                if (CharacterSkinsManager.WaitReplyList.Contains(SkinName))
                {
                    CharacterSkinsManager.WaitReplyList.Remove(SkinName);
                }

                CharacterSkinsManager.SaveSkinToFile(SkinName, _skinData);
                break;
            //响应谁有这个资源
            case DataType.WhoHas:
                if (CharacterSkinsManager.HasSkinRes(SkinName))
                {
                    netNode.QueuePackage(new ComponentClothingPackage(SkinName, DataType.WhoHasReply));
                }

                break;
        }
    }
}
