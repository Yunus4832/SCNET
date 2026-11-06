using EntitySystem.Core;

namespace Game.Network.Handling;

public sealed class ServerInfoPackageHandler(Func<Project, ServerInfoResponsePackage> capture)
    : PackageHandlerBase<ServerInfoPackage>
{
    public override void Handle(ServerInfoPackage package, PackageReceiveContext context)
    {
        if (context.IsServer &&
            package.RequestInfo &&
            context.Project is { } project &&
            context.Sender?.IPPoint is { } endpoint)
        {
            context.Node?.SendWriterFromPackage(capture(project), endpoint);
        }
    }
}
