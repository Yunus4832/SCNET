namespace Game.Network.Handling;

public sealed class ComponentClothingPackageHandler(CharacterSkinStore skins)
    : PackageHandlerBase<ComponentClothingPackage>
{
    private readonly HashSet<string> _pendingSkinQueries = new(StringComparer.Ordinal);

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
                if (skins.Contains(message1.SkinName))
                {
                    netNode.QueuePackage(
                        ClothingPackageBuilder.Capture(
                            skins,
                            message1.SkinName,
                            ComponentClothingPackage.DataType.ReplySkin
                        ),
                        PackageAudience.To(context.Sender!)
                    );
                }
                else
                {
                    if (_pendingSkinQueries.Add(message1.SkinName))
                    {
                        netNode.QueuePackage(
                            ClothingPackageBuilder.Capture(
                                skins,
                                message1.SkinName,
                                ComponentClothingPackage.DataType.WhoHas
                            ),
                            PackageAudience.Global
                        );
                    }
                }

                break;
            }
            case SkinAvailabilityReplyPackage message2:
            {
                _pendingSkinQueries.Remove(message2.SkinName);

                skins.Save(message2.SkinName, message2.SkinData);
                break;
            }
            case SkinReplyPackage message3:
            {
                _pendingSkinQueries.Remove(message3.SkinName);

                skins.Save(message3.SkinName, message3.SkinData);
                break;
            }
            case SkinAvailabilityRequestPackage message4:
            {
                if (skins.Contains(message4.SkinName))
                {
                    NetworkSender.SendToServer(
                        context.Runtime,
                        ClothingPackageBuilder.Capture(
                            skins,
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
