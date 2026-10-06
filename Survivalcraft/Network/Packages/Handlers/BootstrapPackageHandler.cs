using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class BootstrapPackageHandler : PackageHandlerBase<BootstrapPackage>
{
    public override void Handle(BootstrapPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (isServer || netNode == null)
        {
            return;
        }

        PackageDispatcher.Handle(package.ClientList, context);
        netNode.ConnectionEpoch = package.Epoch;
        netNode.CurrentConnectionPhase = ConnectionPhase.BootstrapSent;
        var loadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!;
        loadingScreen.ReplyCall(package.TextureData.Length > 0, package.TextureData, package.ProjectData);
    }
}
