namespace Game.Network.Handling;

/// <summary>
///     Receives server-authored messages. Client chat requests use commands.
/// </summary>
public sealed class MessagePackageHandler : PackageHandlerBase<MessagePackage>
{
    public override void Handle(MessagePackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (isServer ||
            netNode is null ||
            context.Project is not { } project)
        {
            return;
        }

        project.FindSubsystem<SubsystemMessages>(true)!.Service.Receive(package.GameMessage);
    }
}
