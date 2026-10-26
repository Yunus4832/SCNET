using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class BootstrapPackageHandler : PackageHandlerBase<BootstrapPackage>
{
    internal static bool CanApply(BootstrapPackage package, NetNode node) =>
        node.AdmissionAccepted &&
        ProtocolEnum.TryDecode<GameMode>(package.World.Mode, out _) &&
        node.CurrentStage == NetNode.Stage.Bootstrapping &&
        node.CurrentConnectionPhase == ConnectionPhase.TransportConnected &&
        node.ConnectionEpoch == Guid.Empty &&
        package.Epoch != Guid.Empty &&
        node.TokenId != Guid.Empty &&
        package.ClientList.PackageEventType == ClientPackage.EventType.SyncList &&
        package.ClientList.List.Any(client => client.ID != 0 && client.TokenId == node.TokenId);

    public override void Handle(BootstrapPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (isServer || netNode == null || !CanApply(package, netNode))
        {
            return;
        }

        PackageDispatcher.Handle(package.ClientList, context);
        netNode.ConnectionEpoch = package.Epoch;
        netNode.CurrentConnectionPhase = ConnectionPhase.BootstrapSent;
        var loadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!;
        loadingScreen.ReplyCall(
            package.TextureData.Length > 0,
            package.TextureData,
            package.ProjectData,
            new Game.Modding.Blocks.BlockIndexMapping(
                package.BlockMapping.Select(entry =>
                    new KeyValuePair<ResourceId, int>(entry.Id, entry.Index)
                )
            )
        );
    }
}
