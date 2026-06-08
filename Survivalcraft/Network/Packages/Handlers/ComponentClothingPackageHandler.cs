using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentClothingPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        switch (Type)
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

                CharacterSkinsManager.SaveSkinToFile(SkinName, SkinData);
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

public sealed class ComponentClothingPackageHandler : PackageHandlerBase<ComponentClothingPackage>
{
    public override void Handle(ComponentClothingPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentClothingPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
