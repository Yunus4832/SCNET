namespace Game.Network.Packages.Handlers;

public sealed class ServerInfoPackageHandler : PackageHandlerBase<ServerInfoPackage>
{
    public override void Handle(ServerInfoPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        if (package.RequestInfo)
        {
            if (context.Sender?.IPPoint != null)
            {
                netNode?.SendWriterFromPackage(new ServerInfoPackage(false), context.Sender.IPPoint);
            }
        }
    }
}
