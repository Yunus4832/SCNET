using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentClothingPackageHandler : PackageHandlerBase<ComponentClothingPackage>
{
    internal static bool IsValidResourceName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name is not ("." or "..") &&
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

        if (!IsValidResourceName(package.SkinName) ||
            !AcceptsDirection(package.Type, context.IsServer) ||
            (context.IsServer && context.Sender is null))
        {
            return;
        }

        switch (package)
        {
            case SkinRequestPackage message1:
            {
                if (CharacterSkinsManager.HasSkinRes(message1.SkinName))
                {
                    netNode.QueuePackage(
                        ClothingPackageBuilder.Capture(
                            message1.SkinName,
                            ComponentClothingPackage.DataType.ReplySkin
                        ),
                        PackageAudience.To(context.Sender!)
                    );
                }
                else
                {
                    if (!CharacterSkinsManager.WaitReplyList.Contains(message1.SkinName))
                    {
                        netNode.QueuePackage(
                            ClothingPackageBuilder.Capture(
                                message1.SkinName,
                                ComponentClothingPackage.DataType.WhoHas
                            ),
                            PackageAudience.Global
                        );
                        CharacterSkinsManager.WaitReplyList.Add(message1.SkinName);
                    }
                }

                break;
            }
            case SkinAvailabilityReplyPackage message2:
            {
                if (CharacterSkinsManager.WaitReplyList.Contains(message2.SkinName))
                {
                    CharacterSkinsManager.WaitReplyList.Remove(message2.SkinName);
                }

                CharacterSkinsManager.SaveSkinToFile(message2.SkinName, message2.SkinData);
                break;
            }
            case SkinReplyPackage message3:
            {
                if (CharacterSkinsManager.WaitReplyList.Contains(message3.SkinName))
                {
                    CharacterSkinsManager.WaitReplyList.Remove(message3.SkinName);
                }

                CharacterSkinsManager.SaveSkinToFile(message3.SkinName, message3.SkinData);
                break;
            }
            case SkinAvailabilityRequestPackage message4:
            {
                if (CharacterSkinsManager.HasSkinRes(message4.SkinName))
                {
                    NetworkSender.SendToServer(
                        ClothingPackageBuilder.Capture(
                            message4.SkinName,
                            ComponentClothingPackage.DataType.WhoHasReply
                        )
                    );
                }

                break;
            }
        }
    }
}
