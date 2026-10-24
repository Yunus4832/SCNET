using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class CharacterListPackageHandler : PackageHandlerBase<CharacterListPackage>
{
    public override void Handle(CharacterListPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || GameManager.Project is null)
        {
            return;
        }

        GameManager.Project
            .FindSubsystem<SubsystemCharacters>(true)!
            .ApplyCharacterList(package.Characters);
    }
}
