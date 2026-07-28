namespace Game.Network.Packages.Handlers;

public sealed class OnlinePlayerStatePackageHandler : PackageHandlerBase<OnlinePlayerStatePackage>
{
    public override void Handle(OnlinePlayerStatePackage package, NetNode? netNode, bool isServer)
    {
        if (isServer || GameManager.Project is null)
        {
            return;
        }

        GameManager.Project
            .FindSubsystem<SubsystemPlayers>(true)!
            .ApplyOnlinePlayerStates(package.Players);
    }
}
