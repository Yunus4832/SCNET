namespace Game.Network.Packages.Handlers;

public sealed class ServerInfoPackageHandler : PackageHandlerBase<ServerInfoPackage>
{
    public override void Handle(ServerInfoPackage package, NetNode? netNode, bool isServer)
    {
        if (package.RequestInfo)
        {
            if (package.From?.IPPoint != null)
            {
                netNode?.SendWriterFromPackage(new ServerInfoPackage(false), package.From.IPPoint);
            }
        }
    }
}
