namespace Game.Network.Packages.Handlers;

public sealed class ComponentClothingPackageHandler : PackageHandlerBase<ComponentClothingPackage>
{
    internal static bool IsValidResourceName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && name is not ("." or "..") &&
               name.IndexOfAny(['/', '\\', ':', '\0']) < 0;
    }

    internal static bool AcceptsDirection(ComponentClothingPackage.DataType type, bool isServer)
    {
        return isServer
            ? type is ComponentClothingPackage.DataType.RequestSkin or ComponentClothingPackage.DataType.WhoHasReply
            : type is ComponentClothingPackage.DataType.ReplySkin or ComponentClothingPackage.DataType.WhoHas;
    }

    public override void Handle(ComponentClothingPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ComponentClothingPackage)}");
            return;
        }

        if (!IsValidResourceName(package.SkinName) || !AcceptsDirection(package.Type, context.IsServer) ||
            (context.IsServer && context.Sender is null))
        {
            return;
        }

        switch (package.Type)
        {
            case ComponentClothingPackage.DataType.RequestSkin:
                if (CharacterSkinsManager.HasSkinRes(package.SkinName))
                {
                    netNode.QueuePackage(new ComponentClothingPackage(package.SkinName,
                        ComponentClothingPackage.DataType.ReplySkin), PackageAudience.To(context.Sender!));
                }
                else
                {
                    if (!CharacterSkinsManager.WaitReplyList.Contains(package.SkinName))
                    {
                        netNode.QueuePackage(new ComponentClothingPackage(package.SkinName,
                            ComponentClothingPackage.DataType.WhoHas), PackageAudience.Global);
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
                    NetworkSender.SendToServer(new ComponentClothingPackage(package.SkinName,
                        ComponentClothingPackage.DataType.WhoHasReply));
                }

                break;
        }
    }
}
