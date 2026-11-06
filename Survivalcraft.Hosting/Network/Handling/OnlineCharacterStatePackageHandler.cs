namespace Game.Network.Handling;

public sealed class OnlineCharacterStatePackageHandler : PackageHandlerBase<OnlineCharacterStatePackage>
{
    public override void Handle(OnlineCharacterStatePackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || context.Project is null)
        {
            return;
        }

        context.Project
            .FindSubsystem<SubsystemCharacters>(true)!
            .ApplyOnlineCharacterStates(package.Characters);
    }
}
