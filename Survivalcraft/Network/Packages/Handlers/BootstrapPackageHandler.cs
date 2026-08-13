using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class BootstrapPackageHandler : PackageHandlerBase<BootstrapPackage>
{
    public override void Handle(BootstrapPackage package, NetNode? netNode, bool isServer)
    {
        if (isServer || netNode == null)
        {
            return;
        }

        package.ClientList.From = package.From;
        PackageDispatcher.Handle(package.ClientList, netNode, false);
        netNode.ConnectionEpoch = package.Epoch;
        netNode.CurrentConnectionPhase = ConnectionPhase.BootstrapSent;
        var loadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!;
        loadingScreen.ReplyCall(package.TextureData.Length > 0, package.TextureData, package.ProjectData);
    }
}
