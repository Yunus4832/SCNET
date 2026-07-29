namespace Game.Network.Packages.Handlers;

/// <summary>
/// Receives server-authored messages. Client chat requests use commands.
/// </summary>
public sealed class MessagePackageHandler : PackageHandlerBase<MessagePackage>
{
    public override void Handle(MessagePackage package, NetNode? netNode, bool isServer)
    {
        if (isServer ||
            netNode is null ||
            GameManager.Project is not { } project)
        {
            return;
        }

        project.FindSubsystem<SubsystemGameWidgets>(true)!
            .Messages.Receive(package.GameMessage);
    }
}
