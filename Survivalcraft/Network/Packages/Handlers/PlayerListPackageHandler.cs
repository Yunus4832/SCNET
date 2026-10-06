namespace Game.Network.Packages.Handlers;

public sealed class PlayerListPackageHandler : PackageHandlerBase<PlayerListPackage>
{
    public override void Handle(PlayerListPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || GameManager.Project is null)
        {
            return;
        }

        GameManager.Project
            .FindSubsystem<SubsystemPlayers>(true)!
            .ApplyPlayerList(package.Players);
    }
}
