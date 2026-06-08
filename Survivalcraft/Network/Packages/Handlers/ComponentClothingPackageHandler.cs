namespace Game.Network.Packages.Handlers;

public sealed class ComponentClothingPackageHandler : PackageHandlerBase<ComponentClothingPackage>
{
    public override void Handle(ComponentClothingPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentClothingPackage)}");
            return;
        }

        switch (package.Type)
        {
            case ComponentClothingPackage.DataType.RequestSkin:
                if (CharacterSkinsManager.HasSkinRes(package.SkinName))
                {
                    netNode.QueuePackage(new ComponentClothingPackage(package.SkinName,
                        ComponentClothingPackage.DataType.ReplySkin));
                }
                else
                {
                    if (!CharacterSkinsManager.WaitReplyList.Contains(package.SkinName))
                    {
                        netNode.QueuePackage(new ComponentClothingPackage(package.SkinName,
                            ComponentClothingPackage.DataType.WhoHas));
                        CharacterSkinsManager.WaitReplyList.Add(package.SkinName);
                    }
                }

                break;
            // 储存回复的资源
            case ComponentClothingPackage.DataType.WhoHasReply:
            case ComponentClothingPackage.DataType.ReplySkin:
                if (CharacterSkinsManager.WaitReplyList.Contains(package.SkinName))
                {
                    CharacterSkinsManager.WaitReplyList.Remove(package.SkinName);
                }

                CharacterSkinsManager.SaveSkinToFile(package.SkinName, package.SkinData);
                break;
            //响应谁有这个资源
            case ComponentClothingPackage.DataType.WhoHas:
                if (CharacterSkinsManager.HasSkinRes(package.SkinName))
                {
                    netNode.QueuePackage(new ComponentClothingPackage(package.SkinName,
                        ComponentClothingPackage.DataType.WhoHasReply));
                }

                break;
        }
    }
}
