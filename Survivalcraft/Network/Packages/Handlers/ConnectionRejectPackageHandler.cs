using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ConnectionRejectPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        ModFileService.Utils.HandleModDataValidationMessage(Reason);
        netNode.Stop($"[连接拒绝]{Reason}");
    }
}

public sealed class ConnectionRejectPackageHandler : PackageHandlerBase<ConnectionRejectPackage>
{
    public override void Handle(ConnectionRejectPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ConnectionRejectPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
