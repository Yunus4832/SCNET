using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class OnlineCharacterStatePackageHandler : PackageHandlerBase<OnlineCharacterStatePackage>
{
    public override void Handle(OnlineCharacterStatePackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || GameManager.Project is null)
        {
            return;
        }

        GameManager.Project
            .FindSubsystem<SubsystemCharacters>(true)!
            .ApplyOnlineCharacterStates(package.Characters);
    }
}
