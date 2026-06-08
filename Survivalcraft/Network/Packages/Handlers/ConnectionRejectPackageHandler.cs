namespace Game.Network.Packages.Handlers;

public sealed class ConnectionRejectPackageHandler : PackageHandlerBase<ConnectionRejectPackage>
{
    public override void Handle(ConnectionRejectPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ConnectionRejectPackage)}");
            return;
        }

        ModFileService.Utils.HandleModDataValidationMessage(package.Reason);
        netNode.Stop($"[连接拒绝]{package.Reason}");
    }
}
