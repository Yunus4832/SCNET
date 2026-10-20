using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ServerInfoPackageHandler : PackageHandlerBase<ServerInfoPackage>
{
    public override void Handle(ServerInfoPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        if (package.RequestInfo)
        {
            if (context.Sender?.IPPoint != null)
            {
                netNode?.SendWriterFromPackage(ServerInfoPackageBuilder.Capture(), context.Sender.IPPoint);
            }
        }
    }
}
