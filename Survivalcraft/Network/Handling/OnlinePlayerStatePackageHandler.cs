using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class OnlinePlayerStatePackageHandler : PackageHandlerBase<OnlinePlayerStatePackage>
{
    public override void Handle(OnlinePlayerStatePackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || GameManager.Project is null)
        {
            return;
        }

        GameManager.Project
            .FindSubsystem<SubsystemPlayers>(true)!
            .ApplyOnlinePlayerStates(package.Players);
    }
}
